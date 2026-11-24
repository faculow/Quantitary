using System.Diagnostics;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Quantitary.Models;

namespace Quantitary.Controllers;

public class HomeController : Controller
{
    private readonly ProductoRepository _inventoryRepository;
    private readonly IConfiguration _configuration;

    public HomeController(ProductoRepository inventoryRepository, IConfiguration configuration)
    {
        _inventoryRepository = inventoryRepository;
        _configuration = configuration;
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

    private SqlConnection CrearConexion()
    {
        return new SqlConnection(BD.ObtenerCadenaConexion(_configuration));
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
        ViewBag.Dashboard = _inventoryRepository.ObtenerDashboard("mes", empresaId);
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
        using var connection = CrearConexion();
        await connection.OpenAsync();

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

        using var connection = CrearConexion();
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
                VentasMes = cantidadVendida,
                InteresCompraMes = cantidadVendida,
                Rotacion = rotacion,
                Reposicion = reposicion
            };
        }).ToList();

        ViewBag.AnalisisProductos = resumen;
        ViewBag.FechaInicio = inicio;
        ViewBag.FechaFin = fin;
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

    [HttpGet]
    public IActionResult Reportes(string? busqueda, string? tipoReporte, DateTime? fechaDesde, DateTime? fechaHasta, string? frecuenciaActualizacion, string? periodoTiempo)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var model = new ReporteFormularioViewModel();
        int empresaIdVista = ObtenerEmpresaIdActual();
        ViewBag.ProductosDisponibles = _inventoryRepository.ObtenerProductos(null, null, empresaIdVista);
        var reportesGuardados = ObtenerReportesGuardados();
        ViewBag.UltimoReporte = reportesGuardados.FirstOrDefault();

        var busquedaNormalizada = string.IsNullOrWhiteSpace(busqueda) ? null : busqueda.Trim();

        if (fechaDesde.HasValue && fechaHasta.HasValue && fechaHasta.Value.Date < fechaDesde.Value.Date)
        {
            var fechaIntermedia = fechaDesde.Value;
            fechaDesde = fechaHasta;
            fechaHasta = fechaIntermedia;
        }

        var reportesFiltrados = reportesGuardados.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(busquedaNormalizada))
        {
            reportesFiltrados = reportesFiltrados.Where(reporte =>
                reporte.Nombre.Contains(busquedaNormalizada, StringComparison.CurrentCultureIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(tipoReporte))
        {
            reportesFiltrados = reportesFiltrados.Where(reporte =>
                string.Equals(reporte.TipoReporte, tipoReporte, StringComparison.OrdinalIgnoreCase));
        }

        if (fechaDesde.HasValue)
        {
            reportesFiltrados = reportesFiltrados.Where(reporte =>
                reporte.FechaCreacion.Date >= fechaDesde.Value.Date);
        }

        if (fechaHasta.HasValue)
        {
            reportesFiltrados = reportesFiltrados.Where(reporte =>
                reporte.FechaCreacion.Date <= fechaHasta.Value.Date);
        }

        if (!string.IsNullOrWhiteSpace(frecuenciaActualizacion))
        {
            reportesFiltrados = reportesFiltrados.Where(reporte =>
                string.Equals(reporte.FrecuenciaActualizacion, frecuenciaActualizacion, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(periodoTiempo))
        {
            reportesFiltrados = reportesFiltrados.Where(reporte =>
                string.Equals(reporte.PeriodoTiempo, periodoTiempo, StringComparison.OrdinalIgnoreCase));
        }

        var reportes = reportesFiltrados
            .OrderByDescending(reporte => reporte.FechaCreacion)
            .ToList();

        ViewBag.ReportesGuardados = reportes;
        ViewBag.TotalReportes = reportesGuardados.Count;
        ViewBag.ResultadosReportes = reportes.Count;
        ViewBag.Busqueda = busquedaNormalizada;
        ViewBag.TipoReporteFiltro = tipoReporte;
        ViewBag.FechaDesde = fechaDesde;
        ViewBag.FechaHasta = fechaHasta;
        ViewBag.FrecuenciaActualizacionFiltro = frecuenciaActualizacion;
        ViewBag.PeriodoTiempoFiltro = periodoTiempo;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarReporte(ReporteFormularioViewModel modelo)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        int empresaIdVista = ObtenerEmpresaIdActual();
        ViewBag.ProductosDisponibles = _inventoryRepository.ObtenerProductos(null, null, empresaIdVista);

        if (modelo.TipoReporte == "Productos" && (modelo.ProductosIds == null || modelo.ProductosIds.Count == 0))
        {
            ModelState.AddModelError(nameof(modelo.ProductosIds), "Seleccioná al menos un producto para generar el reporte.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.ReportesGuardados = ObtenerReportesGuardados();
            ViewBag.UltimoReporte = ViewBag.ReportesGuardados is List<ReporteGuardado> reportes ? reportes.FirstOrDefault() : null;
            return View("Reportes", modelo);
        }

        int usuarioId = BD.ObtenerUsuarioLogueadoID(HttpContext) ?? 0;
        int empresaId = ObtenerEmpresaIdActual();
        string rangoReporte = ObtenerRangoReporte(modelo.PeriodoTiempo);
        (DateTime Inicio, DateTime Fin) fechasReporte = ObtenerFechasReporte(modelo.PeriodoTiempo);
        DashboardSummary dashboard = _inventoryRepository.ObtenerDashboard(rangoReporte, empresaId, fechasReporte.Inicio, fechasReporte.Fin);

        List<Producto> productosReporte = _inventoryRepository.ObtenerProductos(null, null, empresaId);
        List<ProductoAnalisisResumen> productosDetallados = new List<ProductoAnalisisResumen>();

        if (modelo.TipoReporte == "Productos")
        {
            productosReporte = productosReporte
                .Where(producto => modelo.ProductosIds.Contains(producto.Id))
                .ToList();

            using var connection = CrearConexion();
            await connection.OpenAsync();
            var analisis = await BD.ObtenerAnalisisProductoAsync(connection, empresaId, fechasReporte.Inicio, fechasReporte.Fin);
            var ventasPorProducto = analisis.VentasPorProducto
                .GroupBy(venta => venta.ProductoId)
                .ToDictionary(grupo => grupo.Key, grupo => grupo.First());

            productosDetallados = productosReporte.Select(producto =>
            {
                ventasPorProducto.TryGetValue(producto.Id, out var venta);
                decimal ventasTotales = venta?.VentasTotales ?? 0m;
                decimal gastoTotal = venta?.GastoTotal ?? 0m;
                decimal gananciaTotal = venta?.GananciaTotal ?? 0m;
                int cantidadVendida = venta?.CantidadVendida ?? 0;

                return new ProductoAnalisisResumen
                {
                    Id = producto.Id,
                    Nombre = producto.Nombre,
                    Sku = producto.Sku,
                    Categoria = producto.Categoria,
                    Precio = producto.Precio,
                    Costo = producto.CostoCompra,
                    Gasto = gastoTotal,
                    Ganancia = gananciaTotal,
                    Margen = ventasTotales == 0m ? 0m : gananciaTotal / ventasTotales * 100m,
                    Stock = producto.StockActual,
                    StockMinimo = producto.StockMinimo,
                    VentasMes = cantidadVendida,
                    InteresCompraMes = cantidadVendida,
                    Rotacion = producto.StockActual == 0 ? 0m : cantidadVendida / (decimal)producto.StockActual,
                    Reposicion = producto.StockActual <= producto.StockMinimo ? "Reponer" : "Al día"
                };
            }).ToList();
        }

        int stockSobra = productosReporte.Count(producto =>
            producto.StockActual > 0 && producto.StockActual > producto.StockMinimo * 2);
        decimal totalVentasReporte = modelo.TipoReporte == "Productos"
            ? productosDetallados.Sum(producto => producto.Gasto + producto.Ganancia)
            : dashboard.TotalVentasMes;
        decimal totalGananciaReporte = modelo.TipoReporte == "Productos"
            ? productosDetallados.Sum(producto => producto.Ganancia)
            : dashboard.GananciaNetaMes;
        decimal totalGastosReporte = modelo.TipoReporte == "Productos"
            ? productosDetallados.Sum(producto => producto.Gasto)
            : dashboard.GastosTotalesMes;
        decimal cantidadVendidaReporte = productosDetallados.Sum(producto => producto.VentasMes);
        decimal margenNeto = totalVentasReporte == 0
            ? 0
            : totalGananciaReporte / totalVentasReporte * 100m;

        string hallazgos = modelo.TipoReporte == "Productos"
            ? $"{productosReporte.Count} productos seleccionados: {productosReporte.Count(producto => producto.StockActual <= producto.StockMinimo)} requieren reposición y {stockSobra} tienen stock sobrante."
            : $"{dashboard.ProductosSinStock} productos sin stock, {dashboard.ProductosPocoStock} con poco stock y {stockSobra} con stock sobrante.";
        string recomendaciones = modelo.TipoReporte == "Productos"
            ? (productosReporte.Count(producto => producto.StockActual <= producto.StockMinimo) > 0
                ? "Reponer primero los productos seleccionados con stock agotado o en el mínimo y revisar sus precios."
                : "Mantener el stock actual y controlar los movimientos de los productos seleccionados.")
            : (dashboard.ProductosSinStock > 0
                ? "Reponer primero los productos agotados y revisar los mínimos de reposición."
                : dashboard.ProductosPocoStock > 0
                    ? "Planificar compras para los productos próximos al mínimo."
                    : "Mantener el stock actual y controlar los productos con movimientos lentos.");

        ReporteGuardado nuevoReporte = new ReporteGuardado
        {
            UsuarioID = usuarioId,
            EmpresaID = empresaId,
            Nombre = modelo.Nombre.Trim(),
            TipoReporte = modelo.TipoReporte,
            PeriodoTiempo = modelo.PeriodoTiempo,
            FormatoReporte = modelo.FormatoReporte,
            FrecuenciaActualizacion = modelo.FrecuenciaActualizacion,
            FechaCreacion = DateTime.Now,
            TotalVentas = totalVentasReporte,
            Ganancia = totalGananciaReporte,
            Gastos = totalGastosReporte,
            VentaPromedio = productosDetallados.Count == 0 ? dashboard.VentaPromedioMes : totalVentasReporte / productosDetallados.Count,
            CantidadProductosVendidos = (int)cantidadVendidaReporte,
            TotalProductos = modelo.TipoReporte == "Productos" ? productosReporte.Count : dashboard.TotalProductos,
            ProductosPocoStock = productosReporte.Count(producto => producto.StockActual > 0 && producto.StockActual <= producto.StockMinimo),
            ProductosSinStock = productosReporte.Count(producto => producto.StockActual <= 0),
            MargenNeto = margenNeto,
            StockSobra = stockSobra,
            Hallazgos = hallazgos,
            Recomendaciones = recomendaciones,
            ProductosIds = modelo.ProductosIds.ToList(),
            VistaPreviaHtml = GenerarVistaPrevia(modelo, dashboard, productosDetallados)
        };

        int reporteId = BD.GuardarReporte(_configuration, nuevoReporte);
        if (reporteId <= 0)
        {
            ModelState.AddModelError(string.Empty, "No se pudo guardar el reporte.");
            ViewBag.ReportesGuardados = ObtenerReportesGuardados();
            return View("Reportes", modelo);
        }

        nuevoReporte.Id = reporteId;
        HttpContext.Session.SetString("UltimoReporteID", reporteId.ToString());
        TempData["ReporteGuardadoExito"] = "El reporte se guardó correctamente.";
        return RedirectToAction(nameof(Reportes));
    }

    [HttpGet]
    public IActionResult VistaPreviaReporte(int? id)
    {
        IActionResult? redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        int usuarioId = BD.ObtenerUsuarioLogueadoID(HttpContext) ?? 0;
        int empresaId = ObtenerEmpresaIdActual();
        int reporteId = id ?? ObtenerUltimoReporteId();
        ReporteGuardado? reporte = BD.ObtenerReporte(_configuration, reporteId, usuarioId, empresaId);

        if (reporte is null)
        {
            return RedirectToAction(nameof(Reportes));
        }

        return Content(reporte.VistaPreviaHtml, "text/html");
    }

    [HttpGet]
    public IActionResult DescargarReporte(int? id)
    {
        IActionResult? redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        int usuarioId = BD.ObtenerUsuarioLogueadoID(HttpContext) ?? 0;
        int empresaId = ObtenerEmpresaIdActual();
        int reporteId = id ?? ObtenerUltimoReporteId();
        ReporteGuardado? reporte = BD.ObtenerReporte(_configuration, reporteId, usuarioId, empresaId);

        if (reporte is null)
        {
            return RedirectToAction(nameof(Reportes));
        }

        string nombreArchivo = SanearNombre(reporte.Nombre);

        if (reporte.FormatoReporte == "Excel")
        {
            byte[] excel = GenerarExcelReporte(reporte);
            return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo + ".xlsx");
        }

        if (reporte.FormatoReporte == "CSV")
        {
            byte[] csv = GenerarCsvReporte(reporte);
            return File(csv, "text/csv; charset=utf-8", nombreArchivo + ".csv");
        }

        if (reporte.FormatoReporte == "HTML")
        {
            byte[] html = Encoding.UTF8.GetBytes(reporte.VistaPreviaHtml);
            return File(html, "text/html; charset=utf-8", nombreArchivo + ".html");
        }

        byte[] pdf = GenerarPdfReporte(reporte);
        return File(pdf, "application/pdf", nombreArchivo + ".pdf");
    }

    private static byte[] GenerarExcelReporte(ReporteGuardado reporte)
    {
        using var archivo = new MemoryStream();
        using (SpreadsheetDocument documento = SpreadsheetDocument.Create(archivo, SpreadsheetDocumentType.Workbook, true))
        {
            WorkbookPart workbookPart = documento.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            Worksheet worksheet = new Worksheet();
            SheetData sheetData = new SheetData();

            AgregarFilaExcel(sheetData, "Reporte", reporte.Nombre);
            AgregarFilaExcel(sheetData, "Tipo", reporte.TipoReporte);
            AgregarFilaExcel(sheetData, "Periodo", reporte.PeriodoTiempo);
            AgregarFilaExcel(sheetData, "Formato", reporte.FormatoReporte);
            AgregarFilaExcel(sheetData, "Actualización", reporte.FrecuenciaActualizacion);
            AgregarFilaExcel(sheetData, "", "");

            if (reporte.TipoReporte == "Stock" || reporte.TipoReporte == "Productos")
            {
                AgregarFilaExcel(sheetData, "Productos totales", reporte.TotalProductos.ToString());
                AgregarFilaExcel(sheetData, "Productos con poco stock", reporte.ProductosPocoStock.ToString());
                AgregarFilaExcel(sheetData, "Productos sin stock", reporte.ProductosSinStock.ToString());
                AgregarFilaExcel(sheetData, "Stock sobrante", reporte.StockSobra.ToString());
            }
            else
            {
                AgregarFilaExcel(sheetData, "Total ventas", reporte.TotalVentas.ToString("N2"));
                AgregarFilaExcel(sheetData, "Ganancia", reporte.Ganancia.ToString("N2"));
                AgregarFilaExcel(sheetData, "Gastos", reporte.Gastos.ToString("N2"));
                AgregarFilaExcel(sheetData, "Venta promedio", reporte.VentaPromedio.ToString("N2"));
                AgregarFilaExcel(sheetData, "Margen neto", reporte.MargenNeto.ToString("N2") + "%");
            }

            AgregarFilaExcel(sheetData, "", "");
            AgregarFilaExcel(sheetData, "Hallazgos", reporte.Hallazgos);
            AgregarFilaExcel(sheetData, "Recomendaciones", reporte.Recomendaciones);

            worksheet.Append(sheetData);
            worksheetPart.Worksheet = worksheet;

            Sheets sheets = documento.WorkbookPart!.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = documento.WorkbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Reporte"
            });

            workbookPart.Workbook.Save();
        }

        return archivo.ToArray();
    }

    private static void AgregarFilaExcel(SheetData sheetData, string concepto, string valor)
    {
        Row fila = new Row();
        fila.Append(new Cell
        {
            DataType = CellValues.InlineString,
            InlineString = new InlineString { Text = new Text(concepto ?? string.Empty) }
        });
        fila.Append(new Cell
        {
            DataType = CellValues.InlineString,
            InlineString = new InlineString { Text = new Text(valor ?? string.Empty) }
        });
        sheetData.Append(fila);
    }

    private static byte[] GenerarCsvReporte(ReporteGuardado reporte)
    {
        List<string[]> filas = new List<string[]>();

        if (reporte.TipoReporte == "Stock" || reporte.TipoReporte == "Productos")
        {
            filas.Add(new[] { "Nombre", "Tipo", "Periodo", "Formato", "Actualizacion", "ProductosTotales", "ProductosPocoStock", "ProductosSinStock", "StockSobra" });
            filas.Add(new[]
            {
                reporte.Nombre, reporte.TipoReporte, reporte.PeriodoTiempo, reporte.FormatoReporte,
                reporte.FrecuenciaActualizacion, reporte.TotalProductos.ToString(), reporte.ProductosPocoStock.ToString(),
                reporte.ProductosSinStock.ToString(), reporte.StockSobra.ToString()
            });
        }
        else
        {
            filas.Add(new[] { "Nombre", "Tipo", "Periodo", "Formato", "Actualizacion", "TotalVentas", "Ganancia", "Gastos", "VentaPromedio", "MargenNeto" });
            filas.Add(new[]
            {
                reporte.Nombre, reporte.TipoReporte, reporte.PeriodoTiempo, reporte.FormatoReporte,
                reporte.FrecuenciaActualizacion, reporte.TotalVentas.ToString("0.00"), reporte.Ganancia.ToString("0.00"),
                reporte.Gastos.ToString("0.00"), reporte.VentaPromedio.ToString("0.00"), reporte.MargenNeto.ToString("0.00")
            });
        }

        StringBuilder contenido = new StringBuilder();
        foreach (string[] fila in filas)
        {
            List<string> campos = new List<string>();
            foreach (string campo in fila)
            {
                campos.Add("\"" + (campo ?? string.Empty).Replace("\"", "\"\"") + "\"");
            }
            contenido.AppendLine(string.Join(",", campos));
        }

        byte[] bom = Encoding.UTF8.GetPreamble();
        byte[] texto = Encoding.UTF8.GetBytes(contenido.ToString());
        byte[] resultado = new byte[bom.Length + texto.Length];
        Buffer.BlockCopy(bom, 0, resultado, 0, bom.Length);
        Buffer.BlockCopy(texto, 0, resultado, bom.Length, texto.Length);
        return resultado;
    }

    private static byte[] GenerarPdfReporte(ReporteGuardado reporte)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        Document document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(style => style.FontSize(10));

                page.Header().Text(reporte.Nombre).FontSize(20).SemiBold().FontColor("#1769E8");

                page.Content().Column(columna =>
                {
                    columna.Spacing(8);
                    columna.Item().Text(texto =>
                    {
                        texto.Span("Tipo: ").SemiBold();
                        texto.Span(reporte.TipoReporte);
                    });
                    columna.Item().Text(texto =>
                    {
                        texto.Span("Periodo: ").SemiBold();
                        texto.Span(reporte.PeriodoTiempo);
                    });
                    columna.Item().Text(texto =>
                    {
                        texto.Span("Formato: ").SemiBold();
                        texto.Span(reporte.FormatoReporte);
                    });
                    columna.Item().Text(texto =>
                    {
                        texto.Span("Actualización: ").SemiBold();
                        texto.Span(reporte.FrecuenciaActualizacion);
                    });

                    columna.Item().PaddingTop(12).Text("Resumen").FontSize(14).SemiBold();

                    if (reporte.TipoReporte == "Stock" || reporte.TipoReporte == "Productos")
                    {
                        columna.Item().Text("Productos totales: " + reporte.TotalProductos);
                        columna.Item().Text("Productos con poco stock: " + reporte.ProductosPocoStock);
                        columna.Item().Text("Productos sin stock: " + reporte.ProductosSinStock);
                        columna.Item().Text("Stock sobrante: " + reporte.StockSobra);
                    }
                    else
                    {
                        columna.Item().Text("Total ventas: $" + reporte.TotalVentas.ToString("N2"));
                        columna.Item().Text("Ganancia: $" + reporte.Ganancia.ToString("N2"));
                        columna.Item().Text("Gastos: $" + reporte.Gastos.ToString("N2"));
                        columna.Item().Text("Venta promedio: $" + reporte.VentaPromedio.ToString("N2"));
                        columna.Item().Text("Margen neto: " + reporte.MargenNeto.ToString("N2") + "%");
                    }

                    if (!string.IsNullOrWhiteSpace(reporte.Hallazgos))
                    {
                        columna.Item().PaddingTop(10).Text("Hallazgos").FontSize(14).SemiBold();
                        columna.Item().Text(reporte.Hallazgos);
                    }

                    if (!string.IsNullOrWhiteSpace(reporte.Recomendaciones))
                    {
                        columna.Item().PaddingTop(10).Text("Recomendaciones").FontSize(14).SemiBold();
                        columna.Item().Text(reporte.Recomendaciones);
                    }
                });

                page.Footer().AlignCenter().Text(texto =>
                {
                    texto.Span("Quantitary - Página ");
                    texto.CurrentPageNumber();
                });
            });
        });

        return document.GeneratePdf();
    }

    private List<ReporteGuardado> ObtenerReportesGuardados()
    {
        int usuarioId = BD.ObtenerUsuarioLogueadoID(HttpContext) ?? 0;
        int empresaId = ObtenerEmpresaIdActual();
        return BD.ObtenerReportes(_configuration, usuarioId, empresaId);
    }

    private int ObtenerUltimoReporteId()
    {
        List<ReporteGuardado> reportes = ObtenerReportesGuardados();
        if (reportes.Count == 0)
        {
            return 0;
        }

        return reportes[0].Id;
    }

    private static string ObtenerRangoReporte(string periodoTiempo)
    {
        return periodoTiempo.Trim().ToLowerInvariant() switch
        {
            "diario" => "dia",
            "semanal" => "semana",
            "mensual" => "mes",
            "trimestral" => "todos",
            "anual" => "año",
            _ => "mes"
        };
    }

    private static (DateTime Inicio, DateTime Fin) ObtenerFechasReporte(string periodoTiempo)
    {
        var hoy = DateTime.Today;

        return periodoTiempo.Trim().ToLowerInvariant() switch
        {
            "diario" => (hoy, hoy),
            "semanal" => (hoy.AddDays(-((int)hoy.DayOfWeek == 0 ? 6 : (int)hoy.DayOfWeek - 1)), hoy),
            "mensual" => (new DateTime(hoy.Year, hoy.Month, 1), hoy),
            "trimestral" => (hoy.AddMonths(-3), hoy),
            "anual" => (new DateTime(hoy.Year, 1, 1), hoy),
            _ => (new DateTime(hoy.Year, hoy.Month, 1), hoy)
        };
    }

    private static string GenerarVistaPrevia(ReporteFormularioViewModel modelo, DashboardSummary dashboard, List<ProductoAnalisisResumen>? productosSeleccionados = null)
    {
        string filas;
        string seccionesDetalle = string.Empty;

        if (modelo.TipoReporte == "Stock")
        {
            filas = $@"
                        <tr><td style='padding: 8px; border: 1px solid #d1d5db;'>Productos totales</td><td style='padding: 8px; border: 1px solid #d1d5db;'>{dashboard.TotalProductos}</td></tr>
                        <tr><td style='padding: 8px; border: 1px solid #d1d5db;'>Stock nulo</td><td style='padding: 8px; border: 1px solid #d1d5db;'>{dashboard.ProductosSinStock}</td></tr>
                        <tr><td style='padding: 8px; border: 1px solid #d1d5db;'>Stock bajo</td><td style='padding: 8px; border: 1px solid #d1d5db;'>{dashboard.ProductosPocoStock}</td></tr>
                        <tr><td style='padding: 8px; border: 1px solid #d1d5db;'>Hallazgo clave</td><td style='padding: 8px; border: 1px solid #d1d5db;'>{dashboard.ProductosSinStock} agotados y {dashboard.ProductosPocoStock} por reponer</td></tr>";
        }
        else if (modelo.TipoReporte == "Productos")
        {
            var seleccionados = productosSeleccionados ?? new List<ProductoAnalisisResumen>();
            decimal ventasSeleccionadas = seleccionados.Sum(producto => producto.Gasto + producto.Ganancia);
            decimal gananciaSeleccionada = seleccionados.Sum(producto => producto.Ganancia);
            decimal gastosSeleccionados = seleccionados.Sum(producto => producto.Gasto);
            decimal margenSeleccionado = ventasSeleccionadas == 0m ? 0m : gananciaSeleccionada / ventasSeleccionadas * 100m;
            int unidadesVendidas = seleccionados.Sum(producto => producto.VentasMes);

            filas = $@"
                        <tr><td style='padding: 8px; border: 1px solid #d1d5db;'>Productos seleccionados</td><td style='padding: 8px; border: 1px solid #d1d5db;'>{seleccionados.Count}</td></tr>
                        <tr><td style='padding: 8px; border: 1px solid #d1d5db;'>Unidades vendidas</td><td style='padding: 8px; border: 1px solid #d1d5db;'>{unidadesVendidas}</td></tr>
                        <tr><td style='padding: 8px; border: 1px solid #d1d5db;'>Ventas del período</td><td style='padding: 8px; border: 1px solid #d1d5db;'>${ventasSeleccionadas.ToString("N2")}</td></tr>
                        <tr><td style='padding: 8px; border: 1px solid #d1d5db;'>Gastos del período</td><td style='padding: 8px; border: 1px solid #d1d5db;'>${gastosSeleccionados.ToString("N2")}</td></tr>
                        <tr><td style='padding: 8px; border: 1px solid #d1d5db;'>Ganancia del período</td><td style='padding: 8px; border: 1px solid #d1d5db;'>${gananciaSeleccionada.ToString("N2")}</td></tr>
                        <tr><td style='padding: 8px; border: 1px solid #d1d5db;'>Margen neto</td><td style='padding: 8px; border: 1px solid #d1d5db;'>{margenSeleccionado.ToString("N2")}%</td></tr>
                        <tr><td style='padding: 8px; border: 1px solid #d1d5db;'>Productos por reponer</td><td style='padding: 8px; border: 1px solid #d1d5db;'>{seleccionados.Count(producto => producto.Stock <= producto.StockMinimo)}</td></tr>";

            var detalle = new StringBuilder();
            detalle.Append("<h4 style='margin-top: 24px;'>Detalle individual de los productos seleccionados</h4>");

            foreach (var producto in seleccionados)
            {
                detalle.Append($@"
                    <div style='border: 1px solid #d1d5db; border-radius: 8px; padding: 12px; margin-top: 12px;'>
                        <h5 style='margin: 0 0 8px 0;'>{System.Net.WebUtility.HtmlEncode(producto.Nombre)}</h5>
                        <table style='width:100%; border-collapse: collapse;'>
                            <tbody>
                                <tr><td style='padding: 6px; border: 1px solid #e5e7eb;'>SKU</td><td style='padding: 6px; border: 1px solid #e5e7eb;'>{System.Net.WebUtility.HtmlEncode(producto.Sku)}</td></tr>
                                <tr><td style='padding: 6px; border: 1px solid #e5e7eb;'>Categoría</td><td style='padding: 6px; border: 1px solid #e5e7eb;'>{System.Net.WebUtility.HtmlEncode(producto.Categoria)}</td></tr>
                                <tr><td style='padding: 6px; border: 1px solid #e5e7eb;'>Precio</td><td style='padding: 6px; border: 1px solid #e5e7eb;'>${producto.Precio.ToString("N2")}</td></tr>
                                <tr><td style='padding: 6px; border: 1px solid #e5e7eb;'>Costo</td><td style='padding: 6px; border: 1px solid #e5e7eb;'>${producto.Costo.ToString("N2")}</td></tr>
                                <tr><td style='padding: 6px; border: 1px solid #e5e7eb;'>Ventas</td><td style='padding: 6px; border: 1px solid #e5e7eb;'>${(producto.Gasto + producto.Ganancia).ToString("N2")}</td></tr>
                                <tr><td style='padding: 6px; border: 1px solid #e5e7eb;'>Gastos</td><td style='padding: 6px; border: 1px solid #e5e7eb;'>${producto.Gasto.ToString("N2")}</td></tr>
                                <tr><td style='padding: 6px; border: 1px solid #e5e7eb;'>Ganancia</td><td style='padding: 6px; border: 1px solid #e5e7eb;'>${producto.Ganancia.ToString("N2")}</td></tr>
                                <tr><td style='padding: 6px; border: 1px solid #e5e7eb;'>Margen</td><td style='padding: 6px; border: 1px solid #e5e7eb;'>{producto.Margen.ToString("N2")}%</td></tr>
                                <tr><td style='padding: 6px; border: 1px solid #e5e7eb;'>Unidades vendidas</td><td style='padding: 6px; border: 1px solid #e5e7eb;'>{producto.VentasMes}</td></tr>
                                <tr><td style='padding: 6px; border: 1px solid #e5e7eb;'>Stock actual / mínimo</td><td style='padding: 6px; border: 1px solid #e5e7eb;'>{producto.Stock} / {producto.StockMinimo}</td></tr>
                                <tr><td style='padding: 6px; border: 1px solid #e5e7eb;'>Estado de reposición</td><td style='padding: 6px; border: 1px solid #e5e7eb;'>{System.Net.WebUtility.HtmlEncode(producto.Reposicion)}</td></tr>
                            </tbody>
                        </table>
                    </div>");
            }

            seccionesDetalle = detalle.ToString();
        }
        else
        {
            filas = $@"
                        <tr><td style='padding: 8px; border: 1px solid #d1d5db;'>Total ventas</td><td style='padding: 8px; border: 1px solid #d1d5db;'>${dashboard.TotalVentasMes.ToString("N2")}</td></tr>
                        <tr><td style='padding: 8px; border: 1px solid #d1d5db;'>Ganancia</td><td style='padding: 8px; border: 1px solid #d1d5db;'>${dashboard.GananciaNetaMes.ToString("N2")}</td></tr>
                        <tr><td style='padding: 8px; border: 1px solid #d1d5db;'>Gastos</td><td style='padding: 8px; border: 1px solid #d1d5db;'>${dashboard.GastosTotalesMes.ToString("N2")}</td></tr>
                        <tr><td style='padding: 8px; border: 1px solid #d1d5db;'>Venta promedio</td><td style='padding: 8px; border: 1px solid #d1d5db;'>${dashboard.VentaPromedioMes.ToString("N2")}</td></tr>";
        }

        return $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; color: #1f2937;'>
                <h3>{System.Net.WebUtility.HtmlEncode(modelo.Nombre)}</h3>
                <p><strong>Tipo:</strong> {System.Net.WebUtility.HtmlEncode(modelo.TipoReporte)}</p>
                <p><strong>Período:</strong> {System.Net.WebUtility.HtmlEncode(modelo.PeriodoTiempo)}</p>
                <p><strong>Formato:</strong> {System.Net.WebUtility.HtmlEncode(modelo.FormatoReporte)}</p>
                <p><strong>Actualización:</strong> {System.Net.WebUtility.HtmlEncode(modelo.FrecuenciaActualizacion)}</p>
                <hr />
                <h4>Resumen general</h4>
                <table style='width:100%; border-collapse: collapse; margin-top: 12px;'>
                    <thead>
                        <tr style='background: #f3f4f6;'>
                            <th style='padding: 8px; border: 1px solid #d1d5db; text-align: left;'>Concepto</th>
                            <th style='padding: 8px; border: 1px solid #d1d5db; text-align: left;'>Valor</th>
                        </tr>
                    </thead>
                    <tbody>{filas}</tbody>
                </table>
                {seccionesDetalle}
            </div>";
    }

    private static string GenerarContenidoReporte(ReporteGuardado reporte)
    {
        if (reporte.FormatoReporte == "CSV")
        {
            if (reporte.TipoReporte == "Stock" || reporte.TipoReporte == "Productos")
            {
                return "Nombre,Tipo,Periodo,Formato,Actualizacion,ProductosTotales,ProductosPocoStock,ProductosSinStock\n" +
                       $"{reporte.Nombre},{reporte.TipoReporte},{reporte.PeriodoTiempo},{reporte.FormatoReporte},{reporte.FrecuenciaActualizacion},{reporte.TotalProductos},{reporte.ProductosPocoStock},{reporte.ProductosSinStock}\n";
            }

            return "Nombre,Tipo,Periodo,Formato,Actualizacion,TotalVentas,Ganancia,Gastos,VentaPromedio\n" +
                   $"{reporte.Nombre},{reporte.TipoReporte},{reporte.PeriodoTiempo},{reporte.FormatoReporte},{reporte.FrecuenciaActualizacion},{reporte.TotalVentas},{reporte.Ganancia},{reporte.Gastos},{reporte.VentaPromedio}\n";
        }

        if (reporte.FormatoReporte == "HTML")
        {
            return reporte.VistaPreviaHtml;
        }

        if (reporte.TipoReporte == "Stock" || reporte.TipoReporte == "Productos")
        {
            return $"Reporte: {reporte.Nombre}\nTipo: {reporte.TipoReporte}\nPeriodo: {reporte.PeriodoTiempo}\nFormato: {reporte.FormatoReporte}\nActualización: {reporte.FrecuenciaActualizacion}\n\nResumen\n-----\nProductos totales: {reporte.TotalProductos}\nProductos con poco stock: {reporte.ProductosPocoStock}\nProductos sin stock: {reporte.ProductosSinStock}";
        }

        return $"Reporte: {reporte.Nombre}\nTipo: {reporte.TipoReporte}\nPeriodo: {reporte.PeriodoTiempo}\nFormato: {reporte.FormatoReporte}\nActualización: {reporte.FrecuenciaActualizacion}\n\nResumen\n-----\nTotal ventas: ${reporte.TotalVentas.ToString("N2")}\nGanancia: ${reporte.Ganancia.ToString("N2")}\nGastos: ${reporte.Gastos.ToString("N2")}\nVenta promedio: ${reporte.VentaPromedio.ToString("N2")}";
    }

    private static string SanearNombre(string nombre)
    {
        var limpio = new string(nombre.Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == ' ').ToArray()).Trim();
        return string.IsNullOrWhiteSpace(limpio) ? "reporte" : limpio.Replace(' ', '-');
    }

    public IActionResult HistorialReportes(string? busqueda, string? tipoReporte, string? frecuenciaActualizacion, string? periodoTiempo, DateTime? fechaDesde, DateTime? fechaHasta)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        if (fechaDesde.HasValue && fechaHasta.HasValue && fechaHasta.Value.Date < fechaDesde.Value.Date)
        {
            DateTime intermedia = fechaDesde.Value;
            fechaDesde = fechaHasta;
            fechaHasta = intermedia;
        }

        var reportes = ObtenerReportesGuardados().AsEnumerable();

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            reportes = reportes.Where(reporte => reporte.Nombre.Contains(busqueda.Trim(), StringComparison.CurrentCultureIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(tipoReporte))
        {
            reportes = reportes.Where(reporte => string.Equals(reporte.TipoReporte, tipoReporte, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(frecuenciaActualizacion))
        {
            reportes = reportes.Where(reporte => string.Equals(reporte.FrecuenciaActualizacion, frecuenciaActualizacion, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(periodoTiempo))
        {
            reportes = reportes.Where(reporte => string.Equals(reporte.PeriodoTiempo, periodoTiempo, StringComparison.OrdinalIgnoreCase));
        }
        if (fechaDesde.HasValue)
        {
            reportes = reportes.Where(reporte => reporte.FechaCreacion.Date >= fechaDesde.Value.Date);
        }
        if (fechaHasta.HasValue)
        {
            reportes = reportes.Where(reporte => reporte.FechaCreacion.Date <= fechaHasta.Value.Date);
        }

        var resultado = reportes
            .OrderByDescending(reporte => reporte.FechaCreacion)
            .ToList();

        ViewBag.ResultadosReportes = resultado.Count;
        ViewBag.Busqueda = busqueda;
        ViewBag.TipoReporteFiltro = tipoReporte;
        ViewBag.FrecuenciaActualizacionFiltro = frecuenciaActualizacion;
        ViewBag.PeriodoTiempoFiltro = periodoTiempo;
        ViewBag.FechaDesde = fechaDesde;
        ViewBag.FechaHasta = fechaHasta;

        return View("HistorialReportes", resultado);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EliminarReporte(int id)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        int usuarioId = BD.ObtenerUsuarioLogueadoID(HttpContext) ?? 0;
        int empresaId = ObtenerEmpresaIdActual();
        if (BD.EliminarReporte(_configuration, id, usuarioId, empresaId))
        {
            TempData["ReporteEliminado"] = "El reporte se eliminó correctamente.";
        }
        else
        {
            TempData["ReporteEliminadoError"] = "No se pudo eliminar el reporte.";
        }

        return RedirectToAction(nameof(HistorialReportes));
    }
}
