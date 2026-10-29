using System.Diagnostics;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quantitary.Models;

namespace Quantitary.Controllers;

public class HomeController : Controller
{
    private readonly ProductoRepository _inventoryRepository;
    private readonly AppDbContext _context;

    public HomeController(ProductoRepository inventoryRepository, AppDbContext context)
    {
        _inventoryRepository = inventoryRepository;
        _context = context;
    }

    private bool HaySesionActiva()
    {
        return HttpContext.Session.GetInt32("UsuarioID") != null;
    }

    private IActionResult? RedirigirSiNoHaySesion()
    {
        if (!HaySesionActiva())
        {
            return RedirectToAction("Login", "Cuenta");
        }

        return null;
    }

    public IActionResult Index()
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var empresaId = HttpContext.Session.GetInt32("EmpresaID") ?? 0;
        if (empresaId > 0 && !_inventoryRepository.TieneProductos(empresaId))
        {
            ViewBag.MensajeAdvertencia = "Primero debes cargar tu catálogo inicial desde Excel para poder continuar.";
        }

        var nombre = HttpContext.Session.GetString("NombreUsuario") ?? "Usuario";
        var nombreEmpresa = HttpContext.Session.GetString("NombreEmpresa") ?? "Mi empresa";
        ViewBag.NombreUsuario = nombre;
        ViewBag.NombreEmpresa = nombreEmpresa;
        ViewBag.Dashboard = _inventoryRepository.ObtenerDashboard("mes", empresaId);
        ViewBag.UltimaVentaTimestamp = HttpContext.Session.GetString("UltimaVentaTimestamp");
        return View();
    }

    public IActionResult Historial(string rango = "mes", string? nombreProducto = null, string? sku = null, string? categoria = null, string? busqueda = null)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var empresaId = HttpContext.Session.GetInt32("EmpresaID") ?? 0;
        var busquedaNormalizada = string.IsNullOrWhiteSpace(busqueda) ? null : busqueda.Trim();

        if (empresaId > 0 && !_inventoryRepository.TieneProductos(empresaId))
        {
            ViewBag.MensajeAdvertencia = "Primero debes cargar tu catálogo inicial desde Excel para poder continuar.";
        }

        ViewBag.Dashboard = _inventoryRepository.ObtenerDashboard(rango, empresaId, nombreProducto, sku, categoria);
        ViewBag.RangoActual = rango;
        ViewBag.Busqueda = busquedaNormalizada;
        ViewBag.NombreEmpresa = HttpContext.Session.GetString("NombreEmpresa") ?? "Mi empresa";
        ViewBag.Categorias = _inventoryRepository.ObtenerCategorias(empresaId);
        ViewBag.NombreProducto = nombreProducto;
        ViewBag.Sku = sku;
        ViewBag.Categoria = categoria;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> AnalisisVentasGanancias(DateTime? fechaInicio = null, DateTime? fechaFin = null)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var empresaId = HttpContext.Session.GetInt32("EmpresaID") ?? 0;
        var inicio = fechaInicio ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var fin = fechaFin ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month), 23, 59, 59, 999);

        if (fin < inicio)
        {
            var intercambio = inicio;
            inicio = fin;
            fin = intercambio;
        }

        var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();

        var sqlBase = @"
            SELECT COALESCE(SUM(v.precio), 0)
            FROM Ventas v
            INNER JOIN Productos p ON p.id = v.productoId
            WHERE (@EmpresaId = 0 OR p.empresaID = @EmpresaId)
              AND v.fecha >= @Inicio AND v.fecha <= @Fin";

        var ventasTotales = await connection.QuerySingleAsync<decimal>(sqlBase, new { EmpresaId = empresaId, Inicio = inicio, Fin = fin });

        var sqlGastos = @"
            SELECT COALESCE(SUM(v.costo), 0)
            FROM Ventas v
            INNER JOIN Productos p ON p.id = v.productoId
            WHERE (@EmpresaId = 0 OR p.empresaID = @EmpresaId)
              AND v.fecha >= @Inicio AND v.fecha <= @Fin";

        var gastosTotales = await connection.QuerySingleAsync<decimal>(sqlGastos, new { EmpresaId = empresaId, Inicio = inicio, Fin = fin });

        var sqlPromedio = @"
            SELECT COALESCE(AVG(v.precio), 0)
            FROM Ventas v
            INNER JOIN Productos p ON p.id = v.productoId
            WHERE (@EmpresaId = 0 OR p.empresaID = @EmpresaId)
              AND v.fecha >= @Inicio AND v.fecha <= @Fin";

        var ventaPromedio = await connection.QuerySingleAsync<decimal>(sqlPromedio, new { EmpresaId = empresaId, Inicio = inicio, Fin = fin });

        var sqlGanancias = @"
            SELECT COALESCE(SUM(v.precio - v.costo), 0)
            FROM Ventas v
            INNER JOIN Productos p ON p.id = v.productoId
            WHERE (@EmpresaId = 0 OR p.empresaID = @EmpresaId)
              AND v.fecha >= @Inicio AND v.fecha <= @Fin";

        var ganancias = await connection.QuerySingleAsync<decimal>(sqlGanancias, new { EmpresaId = empresaId, Inicio = inicio, Fin = fin });

        var margenNeto = ventasTotales == 0 ? 0m : (ganancias / ventasTotales) * 100m;

        var ventaPorSemana = new List<ChartPoint>();
        var semanaActual = inicio.Date;

        while (semanaActual <= fin.Date)
        {
            var semanaFin = semanaActual.AddDays(6) > fin.Date ? fin.Date : semanaActual.AddDays(6);

            var sqlVentaSemana = @"
                SELECT COALESCE(SUM(v.precio), 0)
                FROM Ventas v
                INNER JOIN Productos p ON p.id = v.productoId
                WHERE (@EmpresaId = 0 OR p.empresaID = @EmpresaId)
                  AND v.fecha >= @SemanaInicio AND v.fecha <= @SemanaFin";

            var totalSemana = await connection.QuerySingleAsync<decimal>(sqlVentaSemana, new
            {
                EmpresaId = empresaId,
                SemanaInicio = semanaActual.Date,
                SemanaFin = semanaFin.Date.AddDays(1).AddTicks(-1)
            });

            ventaPorSemana.Add(new ChartPoint
            {
                Label = $"{semanaActual:dd/MM} - {semanaFin:dd/MM}",
                Valor = totalSemana
            });

            semanaActual = semanaActual.AddDays(7);
        }

        var sqlGananciaPorCategoria = @"
            SELECT p.categoria AS Label, COALESCE(SUM(v.precio - v.costo), 0) AS Valor
            FROM Ventas v
            INNER JOIN Productos p ON p.id = v.productoId
            WHERE (@EmpresaId = 0 OR p.empresaID = @EmpresaId)
              AND v.fecha >= @Inicio AND v.fecha <= @Fin
            GROUP BY p.categoria
            ORDER BY Valor DESC";

        var gananciaPorCategoria = (await connection.QueryAsync<ChartPoint>(sqlGananciaPorCategoria, new
        {
            EmpresaId = empresaId,
            Inicio = inicio,
            Fin = fin
        })).ToList();

        ViewBag.FechaInicio = inicio;
        ViewBag.FechaFin = fin;
        ViewBag.VentasTotales = ventasTotales;
        ViewBag.GastosTotales = gastosTotales;
        ViewBag.VentaPromedio = ventaPromedio;
        ViewBag.Ganancias = ganancias;
        ViewBag.MargenNeto = margenNeto;
        ViewBag.VentaPorSemana = ventaPorSemana;
        ViewBag.GananciaPorCategoria = gananciaPorCategoria;
        return View();
    }

    public IActionResult Privacy()
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpGet]
    public IActionResult DashboardData(string rango = "mes")
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var empresaId = HttpContext.Session.GetInt32("EmpresaID") ?? 0;
        var dashboard = _inventoryRepository.ObtenerDashboard(rango, empresaId);
        var ultimaVenta = HttpContext.Session.GetString("UltimaVentaTimestamp");

        return Json(new
        {
            totalProductos = dashboard.TotalProductos,
            productosPocoStock = dashboard.ProductosPocoStock,
            productosSinStock = dashboard.ProductosSinStock,
            gananciaNetaMes = dashboard.GananciaNetaMes,
            totalVentasMes = dashboard.TotalVentasMes,
            ventaPromedioMes = dashboard.VentaPromedioMes,
            gastosTotalesMes = dashboard.GastosTotalesMes,
            mesActual = dashboard.MesActualLabel,
            ventasTotales = dashboard.VentasTotales,
            ventaPromedioPorMes = dashboard.VentaPromedioPorMes,
            ventasPorPeriodo = dashboard.VentasPorPeriodo.Select(x => new { label = x.Label, valor = x.Valor }),
            participacionVentas = dashboard.ParticipacionVentas.Select(x => new { nombre = x.Nombre, valor = x.Valor }),
            gananciaPorCategoria = dashboard.GananciaPorCategoria.Select(x => new { label = x.Label, valor = x.Valor }),
            actualizadoEn = DateTime.Now,
            ultimaVenta = ultimaVenta,
            ultimaVentaValor = !string.IsNullOrWhiteSpace(ultimaVenta) ? DateTime.Parse(ultimaVenta).ToString("O") : null
        });
    }
}
