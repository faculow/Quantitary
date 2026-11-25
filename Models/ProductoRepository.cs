namespace Quantitary.Models;

public class ProductoRepository
{
    private readonly IConfiguration _configuration;

    public ProductoRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public List<string> ObtenerCategorias(int? empresaId = null)
        => BD.ObtenerCategorias(_configuration, empresaId);

    public List<Producto> ObtenerProductos(string? sku = null, string? categoria = null, bool? pocoStock = null, decimal? precioMinimo = null, string? orden = null)
        => BD.ObtenerProductos(_configuration, sku, categoria, pocoStock, precioMinimo, orden);

    public List<Producto> ObtenerProductos(string? sku = null, string? categoria = null, bool? pocoStock = null, decimal? precioMinimo = null, string? orden = null, int? empresaId = null)
        => BD.ObtenerProductos(_configuration, sku, categoria, pocoStock, precioMinimo, orden, empresaId);

    public List<Producto> ObtenerProductos(string? sku = null, string? categoria = null, int? empresaId = null)
        => BD.ObtenerProductos(_configuration, sku, categoria, null, null, null, empresaId);

    public Producto? BuscarPorId(int id)
        => BD.BuscarPorId(_configuration, id);

    public Producto AgregarProducto(Producto producto)
        => BD.AgregarProducto(_configuration, producto);

    public bool ActualizarProducto(int id, Producto producto)
        => BD.ActualizarProducto(_configuration, id, producto);

    public bool EliminarProducto(int id)
        => BD.EliminarProducto(_configuration, id);

    public bool AgregarStock(int id, int cantidad, decimal precioUnitario)
        => BD.AgregarStock(_configuration, id, cantidad, precioUnitario);

    public DashboardSummary ObtenerDashboard(string range, int? empresaId = null)
        => BD.ObtenerDashboard(_configuration, range, empresaId);

    public DashboardSummary ObtenerDashboard(string range, int? empresaId = null, string? nombreProducto = null, string? sku = null, string? categoria = null)
        => BD.ObtenerDashboard(_configuration, range, empresaId, nombreProducto, sku, categoria);

    public DashboardSummary ObtenerDashboard(string range, int? empresaId = null, string? nombreProducto = null, string? sku = null, string? categoria = null, DateTime? fechaInicio = null, DateTime? fechaFin = null)
        => BD.ObtenerDashboard(_configuration, range, empresaId, nombreProducto, sku, categoria, fechaInicio, fechaFin);

    public DashboardSummary ObtenerDashboard(string range, int? empresaId, DateTime? fechaInicio, DateTime? fechaFin)
        => BD.ObtenerDashboard(_configuration, range, empresaId, null, null, null, fechaInicio, fechaFin);

    public IndicadoresYOportunidadesViewModel ObtenerIndicadoresYOportunidades(int? empresaId = null)
    {
        List<ProductoIndicadorBase> productos = BD.ObtenerProductosIndicadoresBase(_configuration, empresaId);
        List<ProductoVentaAggregate> ventasPorProducto = BD.ObtenerVentasPorProductoAggregate(_configuration, empresaId);
        List<ProductoCategoriaAggregate> ventasPorCategoria = BD.ObtenerVentasPorCategoriaAggregate(_configuration, empresaId);

        var ventasDict = ventasPorProducto
            .GroupBy(x => x.ProductoId)
            .ToDictionary(g => g.Key, g => g.First());

        var filas = productos.Select(p =>
        {
            var venta = ventasDict.GetValueOrDefault(p.Id);
            var cantidadVendida = venta?.CantidadVendida ?? 0;
            var ventasTotales = venta?.VentasTotales ?? 0m;
            var costoTotal = venta?.CostoTotal ?? 0m;
            var gananciaTotal = ventasTotales - costoTotal;
            var rotacion = cantidadVendida == 0 ? 0m : (cantidadVendida / (decimal)Math.Max(1, p.StockActual + p.StockMinimo));
            var margen = ventasTotales == 0 ? 0m : (gananciaTotal / ventasTotales) * 100m;
            var accion = p.StockActual <= p.StockMinimo ? "Reponer" : cantidadVendida > 0 && p.StockActual > p.StockMinimo * 2 ? "Comprar menos" : "Mantener";
            var abastecimiento = p.StockActual <= p.StockMinimo ? "Reponer en 3-5 días" : p.StockActual > p.StockMinimo * 2 ? "Sobresatura" : "Stock saludable";
            var compra = p.StockActual <= p.StockMinimo ? "En 3 días" : "Dentro de 7 días";
            var compraPlazo = p.StockActual <= p.StockMinimo ? "Dentro de 7 días" : "Después de 7 días";
            var fechaVentaSugerida = p.StockActual > p.StockMinimo * 2 && cantidadVendida > 0 ? "Fin de semana" : "Próximos 7 días";
            var ajustePrecio = margen > 40m ? "Subir precio" : margen < 20m ? "Bajar precio" : "Mantener precio";

            var minimoActual = Math.Max(0, p.StockMinimo);
            var minimoRecomendado = p.StockActual <= p.StockMinimo
                ? Math.Max(minimoActual + 3, minimoActual + Math.Max(2, cantidadVendida))
                : p.StockActual > p.StockMinimo * 2 && cantidadVendida > 0
                    ? Math.Max(0, minimoActual - 2)
                    : minimoActual;

            var minimoTexto = minimoRecomendado > minimoActual
                ? $"Subir a {minimoRecomendado}"
                : minimoRecomendado < minimoActual
                    ? $"Bajar a {minimoRecomendado}"
                    : $"Mantener en {minimoActual}";

            var diaMas = ObtenerDiaMasVendido(p.Categoria);
            var diaMenos = ObtenerDiaMenosVendido(p.Categoria);
            var festividad = ObtenerFestividadRelevante();
            var recomendacionTexto = $"{p.Nombre} vende más los {diaMas} y menos los {diaMenos}. Como la demanda se acelera en {festividad}, conviene reforzar stock antes del fin de semana y hacer foco en promocionar el producto para mover más unidades.";
            var siguientePaso = p.StockActual <= p.StockMinimo
                ? $"Para vender más {p.Nombre}, reponé stock antes del {diaMas} y prepará promoción para ese día."
                : $"Para vender más {p.Nombre}, enfoca promociones desde el {diaMas} y revisá el mínimo antes de la próxima campaña.";

            return new ProductoIndicadorRow
            {
                Id = p.Id,
                Nombre = p.Nombre,
                Categoria = p.Categoria,
                StockActual = p.StockActual,
                StockMinimo = p.StockMinimo,
                StockMinimoRecomendado = minimoRecomendado,
                MinimoReposicionTexto = minimoTexto,
                Precio = p.Precio,
                CostoCompra = p.CostoCompra,
                CantidadVendida = cantidadVendida,
                VentasTotales = ventasTotales,
                GananciaTotal = gananciaTotal,
                Rotacion = rotacion,
                Margen = margen,
                AccionSugerida = accion,
                RecomendacionAbastecimiento = abastecimiento,
                FechaCompraSugerida = compra,
                CompraPlazoTexto = compraPlazo,
                FechaVentaSugerida = fechaVentaSugerida,
                AjustePrecio = ajustePrecio,
                DiaMasVendido = diaMas,
                DiaMenosVendido = diaMenos,
                FestividadesSugeridas = festividad,
                RecomendacionTexto = recomendacionTexto,
                SiguientePaso = siguientePaso
            };
        }).ToList();

        var topCategoria = ventasPorCategoria
            .OrderByDescending(x => x.VentasTotales)
            .FirstOrDefault();

        return new IndicadoresYOportunidadesViewModel
        {
            MargenNetoGeneral = filas.Any() ? (filas.Sum(x => x.GananciaTotal) / Math.Max(1m, filas.Sum(x => x.VentasTotales))) * 100m : 0m,
            ProductoMayorRotacion = filas.OrderByDescending(x => x.Rotacion).FirstOrDefault()?.Nombre ?? "Sin datos",
            RelacionInteresCompra = topCategoria == null ? "Sin datos" : $"{topCategoria.Categoria} lidera las ventas",
            CategoriaMasVentas = topCategoria == null ? "Sin datos" : topCategoria.Categoria,
            TotalProductos = filas.Count,
            ResumenEstado = filas.Count == 0 ? "Sin alertas" : "Revisión activa",
            ProductosReponer = filas.Where(x => x.AccionSugerida == "Reponer").Select(x => x.Nombre).ToList(),
            ProductosComprarMenos = filas.Where(x => x.AccionSugerida == "Comprar menos").Select(x => x.Nombre).ToList(),
            ProductosComprarMas = filas.Where(x => x.AccionSugerida == "Mantener" && x.StockActual <= x.StockMinimo * 1.2m).Select(x => x.Nombre).ToList(),
            ProductosCambiarMinimo = filas.Where(x => x.MinimoReposicionTexto.Contains("Subir") || x.MinimoReposicionTexto.Contains("Bajar")).Select(x => x.Nombre).ToList(),
            RecomendacionesTexto = filas.Select(x => x.RecomendacionTexto).Where(x => !string.IsNullOrWhiteSpace(x)).Take(6).ToList(),
            ExplicacionesIndicadores = filas.Select(x => x.SiguientePaso).Where(x => !string.IsNullOrWhiteSpace(x)).Take(6).ToList(),
            Productos = filas
        };
    }

    public bool TieneProductos(int empresaId)
        => BD.TieneProductos(_configuration, empresaId);

    public IConfiguration GetConfiguration()
        => _configuration;

    private static string ObtenerDiaMasVendido(string categoria)
    {
        if (categoria.Contains("Tecn", StringComparison.OrdinalIgnoreCase) || categoria.Contains("Elect", StringComparison.OrdinalIgnoreCase))
            return "viernes";

        if (categoria.Contains("Hogar", StringComparison.OrdinalIgnoreCase) || categoria.Contains("Muebles", StringComparison.OrdinalIgnoreCase))
            return "sábado";

        if (categoria.Contains("Ropa", StringComparison.OrdinalIgnoreCase) || categoria.Contains("Moda", StringComparison.OrdinalIgnoreCase))
            return "viernes";

        return "sábado";
    }

    private static string ObtenerDiaMenosVendido(string categoria)
    {
        if (categoria.Contains("Tecn", StringComparison.OrdinalIgnoreCase) || categoria.Contains("Elect", StringComparison.OrdinalIgnoreCase))
            return "lunes";

        if (categoria.Contains("Hogar", StringComparison.OrdinalIgnoreCase) || categoria.Contains("Muebles", StringComparison.OrdinalIgnoreCase))
            return "martes";

        if (categoria.Contains("Ropa", StringComparison.OrdinalIgnoreCase) || categoria.Contains("Moda", StringComparison.OrdinalIgnoreCase))
            return "martes";

        return "lunes";
    }

    private static string ObtenerFestividadRelevante()
    {
        var mes = DateTime.Now.Month;

        return mes switch
        {
            11 or 12 => "fin de año",
            1 or 2 => "vacaciones y promociones de verano",
            3 or 4 => "feriados largos y compras de temporada",
            5 or 6 => "temporada de promociones de invierno",
            _ => "mes normal con venta estable"
        };
    }

}
