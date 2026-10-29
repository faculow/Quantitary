using System.Diagnostics;
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

        var inicio = fechaInicio ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var fin = fechaFin ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month), 23, 59, 59, 999);

        if (fin < inicio)
        {
            var intercambio = inicio;
            inicio = fin;
            fin = intercambio;
        }

        var ventasTotales = await _context.Database
            .SqlQuery<decimal>($"SELECT CAST(COALESCE(SUM(precio), 0) AS decimal(18,2)) FROM Ventas WHERE fecha >= {inicio} AND fecha <= {fin}")
            .SingleAsync();

        var ganancias = await _context.Database
            .SqlQuery<decimal>($"SELECT CAST(COALESCE(SUM((precio - costo) * cantidad), 0) AS decimal(18,2)) FROM Ventas WHERE fecha >= {inicio} AND fecha <= {fin}")
            .SingleAsync();

        var margenNeto = ventasTotales == 0 ? 0m : (ganancias / ventasTotales) * 100m;

        ViewBag.FechaInicio = inicio;
        ViewBag.FechaFin = fin;
        ViewBag.VentasTotales = ventasTotales;
        ViewBag.Ganancias = ganancias;
        ViewBag.MargenNeto = margenNeto;
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
