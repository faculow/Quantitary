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

    public class ProductoAnalisisRow
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public decimal CostoCompra { get; set; }
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
        public decimal GastoTotal { get; set; }
        public decimal GananciaTotal { get; set; }
        public int CantidadVendida { get; set; }
        public decimal VentasTotales { get; set; }
    }

    public class ProductoAnalisisResumen
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public decimal Costo { get; set; }
        public decimal Gasto { get; set; }
        public decimal Ganancia { get; set; }
        public decimal Margen { get; set; }
        public int Stock { get; set; }
        public int StockMinimo { get; set; }
        public string Reposicion { get; set; } = string.Empty;
        public decimal Rotacion { get; set; }
        public int VentasMes { get; set; }
        public int InteresCompraMes { get; set; }
    }

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

        ViewBag.FechaInicio = inicio;
        ViewBag.FechaFin = fin;
        ViewBag.VentasTotales = ventasTotales;
        ViewBag.GastosTotales = gastosTotales;
        ViewBag.VentaPromedio = ventaPromedio;
        ViewBag.Ganancias = ganancias;
        ViewBag.MargenNeto = margenNeto;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> AnalisisProducto(DateTime? fechaInicio = null, DateTime? fechaFin = null)
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

        using var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();

        var productos = (await connection.QueryAsync<ProductoAnalisisRow>(@"
            SELECT p.id AS Id,
                   p.nombre AS Nombre,
                   p.sku AS Sku,
                   p.categoria AS Categoria,
                   p.precio AS Precio,
                   p.costoCompra AS CostoCompra,
                   p.stockActual AS StockActual,
                   p.stockMinimo AS StockMinimo,
                   p.cantidadVentas AS CantidadVendida
            FROM Productos p
            WHERE (@EmpresaId = 0 OR p.empresaID = @EmpresaId)
            ORDER BY p.nombre", new { EmpresaId = empresaId })).ToList();

        var ventasPorProducto = (await connection.QueryAsync<dynamic>(@"
            SELECT v.productoId AS ProductoId,
                   SUM(v.cantidad) AS CantidadVendida,
                   SUM(v.precio) AS VentasTotales,
                   SUM(v.costo) AS GastoTotal,
                   SUM(v.precio - v.costo) AS GananciaTotal
            FROM Ventas v
            INNER JOIN Productos p ON p.id = v.productoId
            WHERE (@EmpresaId = 0 OR p.empresaID = @EmpresaId)
              AND v.fecha >= @Inicio AND v.fecha <= @Fin
            GROUP BY v.productoId", new { EmpresaId = empresaId, Inicio = inicio, Fin = fin })).ToList();

        var ventasPorMes = (await connection.QueryAsync<dynamic>(@"
            SELECT DATENAME(month, v.fecha) AS Mes,
                   MONTH(v.fecha) AS MesNumero,
                   YEAR(v.fecha) AS Anio,
                   SUM(v.precio) AS Total,
                   SUM(v.cantidad) AS Cantidad
            FROM Ventas v
            INNER JOIN Productos p ON p.id = v.productoId
            WHERE (@EmpresaId = 0 OR p.empresaID = @EmpresaId)
              AND v.fecha >= @Inicio AND v.fecha <= @Fin
            GROUP BY YEAR(v.fecha), MONTH(v.fecha), DATENAME(month, v.fecha)
            ORDER BY YEAR(v.fecha), MONTH(v.fecha)", new { EmpresaId = empresaId, Inicio = inicio, Fin = fin })).ToList();

        var interesCompraPorMes = (await connection.QueryAsync<dynamic>(@"
            SELECT DATENAME(month, v.fecha) AS Mes,
                   MONTH(v.fecha) AS MesNumero,
                   YEAR(v.fecha) AS Anio,
                   COUNT(v.id) AS InteresCompra,
                   SUM(v.cantidad) AS Cantidad
            FROM Ventas v
            INNER JOIN Productos p ON p.id = v.productoId
            WHERE (@EmpresaId = 0 OR p.empresaID = @EmpresaId)
              AND v.fecha >= @Inicio AND v.fecha <= @Fin
            GROUP BY YEAR(v.fecha), MONTH(v.fecha), DATENAME(month, v.fecha)
            ORDER BY YEAR(v.fecha), MONTH(v.fecha)", new { EmpresaId = empresaId, Inicio = inicio, Fin = fin })).ToList();

        var ventasSemana = (await connection.QueryAsync<dynamic>(@"
            SELECT DATENAME(weekday, v.fecha) AS Dia,
                   SUM(v.precio) AS Total,
                   SUM(v.cantidad) AS Cantidad
            FROM Ventas v
            INNER JOIN Productos p ON p.id = v.productoId
            WHERE (@EmpresaId = 0 OR p.empresaID = @EmpresaId)
              AND v.fecha >= @Inicio AND v.fecha <= @Fin
            GROUP BY DATENAME(weekday, v.fecha)
            ORDER BY CASE DATENAME(weekday, v.fecha)
                WHEN 'Monday' THEN 1
                WHEN 'Tuesday' THEN 2
                WHEN 'Wednesday' THEN 3
                WHEN 'Thursday' THEN 4
                WHEN 'Friday' THEN 5
                WHEN 'Saturday' THEN 6
                WHEN 'Sunday' THEN 7
                ELSE 8 END", new { EmpresaId = empresaId, Inicio = inicio, Fin = fin })).ToList();

        var ventasDict = ventasPorProducto
            .GroupBy(x => (int)x.ProductoId)
            .ToDictionary(g => g.Key, g => g.First());

        var resumen = productos.Select(p =>
        {
            var venta = ventasDict.ContainsKey(p.Id) ? ventasDict[p.Id] : null;
            var ventasTotales = venta != null ? (decimal)venta.VentasTotales : 0m;
            var gastoTotal = venta != null ? (decimal)venta.GastoTotal : 0m;
            var gananciaTotal = venta != null ? (decimal)venta.GananciaTotal : 0m;
            var cantidadVendida = venta != null ? (int)venta.CantidadVendida : 0;
            var margen = ventasTotales == 0 ? 0m : (gananciaTotal / ventasTotales) * 100m;
            var rotacion = p.StockActual == 0 ? 0m : (cantidadVendida / (decimal)Math.Max(1, p.StockActual));
            var reposicion = p.StockActual <= p.StockMinimo ? "Reponer" : "Al día";

            return new ProductoAnalisisResumen
            {
                Id = p.Id,
                Nombre = p.Nombre,
                Sku = p.Sku,
                Categoria = p.Categoria,
                Precio = p.Precio,
                Costo = p.CostoCompra,
                Gasto = gastoTotal,
                Ganancia = gananciaTotal,
                Margen = margen,
                Stock = p.StockActual,
                StockMinimo = p.StockMinimo,
                Reposicion = reposicion,
                Rotacion = rotacion,
                VentasMes = cantidadVendida,
                InteresCompraMes = cantidadVendida
            };
        }).ToList();

        ViewBag.FechaInicio = inicio;
        ViewBag.FechaFin = fin;
        ViewBag.AnalisisProductos = resumen;
        ViewBag.VentasPorMes = ventasPorMes;
        ViewBag.InteresCompraPorMes = interesCompraPorMes;
        ViewBag.VentasSemana = ventasSemana;
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
