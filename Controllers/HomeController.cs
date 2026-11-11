using System.Diagnostics;
using System.Text;
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

    private int ObtenerEmpresaIdActual()
    {
        return BD.ObtenerEmpresaIdActual(_inventoryRepository.GetConfiguration(), HttpContext);
    }

    public IActionResult Index()
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var empresaId = ObtenerEmpresaIdActual();
        if (empresaId > 0 && !_inventoryRepository.TieneProductos(empresaId))
        {
            return RedirectToAction("Importar", "Stock");
        }

        var nombre = HttpContext.Session.GetString("NombreUsuario") ?? "Usuario";
        var nombreEmpresa = HttpContext.Session.GetString("NombreEmpresa") ?? "Mi empresa";
        ViewBag.NombreUsuario = nombre;
        ViewBag.NombreEmpresa = nombreEmpresa;
        ViewBag.Dashboard = _inventoryRepository.ObtenerDashboard("todos", empresaId);
        ViewBag.UltimaVentaTimestamp = HttpContext.Session.GetString("UltimaVentaTimestamp");
        return View("Home");
    }

    [HttpGet]
    public IActionResult Landing()
    {
        return View("Index");
    }

    public IActionResult IndicadoresYOportunidades()
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var empresaId = ObtenerEmpresaIdActual();
        var model = _inventoryRepository.ObtenerIndicadoresYOportunidades(empresaId);

        ViewBag.NombreEmpresa = HttpContext.Session.GetString("NombreEmpresa") ?? "Mi empresa";
        return View(model);
    }

    public IActionResult ExportarResumenIndicadores()
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var empresaId = ObtenerEmpresaIdActual();
        var model = _inventoryRepository.ObtenerIndicadoresYOportunidades(empresaId);

        var csv = new StringBuilder();
        csv.AppendLine("Producto,Categoria,Rotacion,StockActual,StockMinimo,AccionSugerida,FechaCompraSugerida,FechaVentaSugerida,AjustePrecio");

        foreach (var item in model.Productos)
        {
            csv.AppendLine($"\"{item.Nombre}\",\"{item.Categoria}\",{item.Rotacion.ToString("0.##")},{item.StockActual},{item.StockMinimo},\"{item.AccionSugerida}\",\"{item.FechaCompraSugerida}\",\"{item.FechaVentaSugerida}\",\"{item.AjustePrecio}\"");
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", "indicadores_oportunidades.csv");
    }

    [HttpPost]
    public IActionResult ProgramarReporteIndicadores()
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        TempData["ReporteProgramado"] = "El reporte de indicadores quedó programado para el próximo ciclo.";
        return RedirectToAction(nameof(IndicadoresYOportunidades));
    }

    public IActionResult Historial(string rango = "todos", string? nombreProducto = null, string? sku = null, string? categoria = null, string? busqueda = null)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var empresaId = ObtenerEmpresaIdActual();
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

        var empresaId = ObtenerEmpresaIdActual();
        var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();

        // Un período acotado mantiene los gráficos legibles al entrar. El usuario puede
        // ampliar el rango manualmente con los filtros de la pantalla.
        var inicio = fechaInicio ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var fin = fechaFin ?? DateTime.Today;

        if (fin < inicio)
        {
            var intercambio = inicio;
            inicio = fin;
            fin = intercambio;
        }

        var analisis = await BD.ObtenerAnalisisVentasGananciasAsync(connection, empresaId, inicio, fin);

        ViewBag.FechaInicio = inicio;
        ViewBag.FechaFin = fin;
        ViewBag.VentasTotales = analisis.VentasTotales;
        ViewBag.VentasTotalesCantidad = analisis.VentasTotalesCantidad;
        ViewBag.GastosTotales = analisis.GastosTotales;
        ViewBag.VentaPromedio = analisis.VentaPromedio;
        ViewBag.Ganancias = analisis.Ganancias;
        ViewBag.MargenNeto = analisis.MargenNeto;
        ViewBag.VentaPorSemana = analisis.VentaPorSemana;
        ViewBag.VentasDiarias = analisis.VentasDiarias;
        ViewBag.GananciaPorCategoria = analisis.GananciaPorCategoria;
        ViewBag.FormulaGanancia = analisis.FormulaGanancia;
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

        var empresaId = ObtenerEmpresaIdActual();
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

        var analisis = await BD.ObtenerAnalisisProductoAsync(connection, empresaId, inicio, fin);

        var ventasDict = analisis.VentasPorProducto
            .GroupBy(x => x.ProductoId)
            .ToDictionary(g => g.Key, g => g.First());

        var resumen = analisis.Productos.Select(p =>
        {
            var venta = ventasDict.ContainsKey(p.Id) ? ventasDict[p.Id] : null;
            var ventasTotales = venta != null ? venta.VentasTotales : 0m;
            var gastoTotal = venta != null ? venta.GastoTotal : 0m;
            var gananciaTotal = venta != null ? venta.GananciaTotal : 0m;
            var cantidadVendida = venta != null ? venta.CantidadVendida : 0;
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
        ViewBag.VentasPorMes = analisis.VentasPorMes;
        ViewBag.InteresCompraPorMes = analisis.InteresCompraPorMes;
        ViewBag.VentasSemana = analisis.VentasSemana;
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
    public IActionResult VentasPorPeriodoData(string rango = "mes", DateTime? fechaInicio = null, DateTime? fechaFin = null)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var empresaId = ObtenerEmpresaIdActual();
        var dashboard = _inventoryRepository.ObtenerDashboard(rango, empresaId, null, null, null, fechaInicio, fechaFin);

        return Json(new
        {
            ventasPorPeriodo = dashboard.VentasPorPeriodo.Select(x => new { label = x.Label, valor = x.Valor })
        });
    }

    [HttpGet]
    public IActionResult DashboardData(string rango = "todos", DateTime? fechaInicio = null, DateTime? fechaFin = null, string? granularidad = null, bool soloVentasPeriodo = false)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var empresaId = ObtenerEmpresaIdActual();
        var rangoUsado = string.IsNullOrWhiteSpace(granularidad) ? rango : granularidad;
        var dashboard = _inventoryRepository.ObtenerDashboard(rangoUsado, empresaId, null, null, null, fechaInicio, fechaFin);
        var ultimaVenta = HttpContext.Session.GetString("UltimaVentaTimestamp");

        if (soloVentasPeriodo)
        {
            return Json(new
            {
                ventasPorPeriodo = dashboard.VentasPorPeriodo.Select(x => new { label = x.Label, valor = x.Valor }),
                granularidad = rangoUsado,
                rango = rangoUsado,
                actualizadoEn = DateTime.Now,
                ultimaVenta = ultimaVenta,
                ultimaVentaValor = !string.IsNullOrWhiteSpace(ultimaVenta) ? DateTime.Parse(ultimaVenta).ToString("O") : null
            });
        }

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
