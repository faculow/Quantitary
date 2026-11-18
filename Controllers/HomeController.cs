using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
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
    public IActionResult GuardarReporte(ReporteFormularioViewModel modelo)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        if (!ModelState.IsValid)
        {
            ViewBag.ReportesGuardados = ObtenerReportesGuardados();
            return View("Reportes", modelo);
        }

        int usuarioId = BD.ObtenerUsuarioLogueadoID(HttpContext) ?? 0;
        int empresaId = ObtenerEmpresaIdActual();
        string rangoReporte = ObtenerRangoReporte(modelo.PeriodoTiempo);
        (DateTime Inicio, DateTime Fin) fechasReporte = ObtenerFechasReporte(modelo.PeriodoTiempo);
        DashboardSummary dashboard = _inventoryRepository.ObtenerDashboard(rangoReporte, empresaId, fechasReporte.Inicio, fechasReporte.Fin);

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
            TotalVentas = dashboard.TotalVentasMes,
            Ganancia = dashboard.GananciaNetaMes,
            Gastos = dashboard.GastosTotalesMes,
            VentaPromedio = dashboard.VentaPromedioMes,
            CantidadProductosVendidos = dashboard.UltimosProductosVendidos.Sum(item => item.Cantidad),
            TotalProductos = dashboard.TotalProductos,
            ProductosPocoStock = dashboard.ProductosPocoStock,
            ProductosSinStock = dashboard.ProductosSinStock,
            VistaPreviaHtml = GenerarVistaPrevia(modelo, dashboard)
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

        string contenido = GenerarContenidoReporte(reporte);
        string extension = reporte.FormatoReporte switch
        {
            "Excel" => ".xlsx",
            "CSV" => ".csv",
            "HTML" => ".html",
            _ => ".pdf"
        };

        string mime = reporte.FormatoReporte switch
        {
            "Excel" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "CSV" => "text/csv",
            "HTML" => "text/html",
            _ => "application/pdf"
        };

        return File(Encoding.UTF8.GetBytes(contenido), mime, $"{SanearNombre(reporte.Nombre)}{extension}");
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

    private static string GenerarVistaPrevia(ReporteFormularioViewModel modelo, DashboardSummary dashboard)
    {
        string filas;

        if (modelo.TipoReporte == "Stock" || modelo.TipoReporte == "Productos")
        {
            filas = $@"
                        <tr>
                            <td style='padding: 8px; border: 1px solid #d1d5db;'>Productos totales</td>
                            <td style='padding: 8px; border: 1px solid #d1d5db;'>{dashboard.TotalProductos}</td>
                        </tr>
                        <tr>
                            <td style='padding: 8px; border: 1px solid #d1d5db;'>Productos con poco stock</td>
                            <td style='padding: 8px; border: 1px solid #d1d5db;'>{dashboard.ProductosPocoStock}</td>
                        </tr>
                        <tr>
                            <td style='padding: 8px; border: 1px solid #d1d5db;'>Productos sin stock</td>
                            <td style='padding: 8px; border: 1px solid #d1d5db;'>{dashboard.ProductosSinStock}</td>
                        </tr>";
        }
        else
        {
            filas = $@"
                        <tr>
                            <td style='padding: 8px; border: 1px solid #d1d5db;'>Total ventas</td>
                            <td style='padding: 8px; border: 1px solid #d1d5db;'>${dashboard.TotalVentasMes.ToString("N2")}</td>
                        </tr>
                        <tr>
                            <td style='padding: 8px; border: 1px solid #d1d5db;'>Ganancia</td>
                            <td style='padding: 8px; border: 1px solid #d1d5db;'>${dashboard.GananciaNetaMes.ToString("N2")}</td>
                        </tr>
                        <tr>
                            <td style='padding: 8px; border: 1px solid #d1d5db;'>Gastos</td>
                            <td style='padding: 8px; border: 1px solid #d1d5db;'>${dashboard.GastosTotalesMes.ToString("N2")}</td>
                        </tr>
                        <tr>
                            <td style='padding: 8px; border: 1px solid #d1d5db;'>Venta promedio</td>
                            <td style='padding: 8px; border: 1px solid #d1d5db;'>${dashboard.VentaPromedioMes.ToString("N2")}</td>
                        </tr>";
        }

        return $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; color: #1f2937;'>
                <h3>{System.Net.WebUtility.HtmlEncode(modelo.Nombre)}</h3>
                <p><strong>Tipo:</strong> {System.Net.WebUtility.HtmlEncode(modelo.TipoReporte)}</p>
                <p><strong>Período:</strong> {System.Net.WebUtility.HtmlEncode(modelo.PeriodoTiempo)}</p>
                <p><strong>Formato:</strong> {System.Net.WebUtility.HtmlEncode(modelo.FormatoReporte)}</p>
                <p><strong>Actualización:</strong> {System.Net.WebUtility.HtmlEncode(modelo.FrecuenciaActualizacion)}</p>
                <hr />
                <table style='width:100%; border-collapse: collapse; margin-top: 12px;'>
                    <thead>
                        <tr style='background: #f3f4f6;'>
                            <th style='padding: 8px; border: 1px solid #d1d5db; text-align: left;'>Concepto</th>
                            <th style='padding: 8px; border: 1px solid #d1d5db; text-align: left;'>Valor</th>
                        </tr>
                    </thead>
                    <tbody>{filas}</tbody>
                </table>
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
}
