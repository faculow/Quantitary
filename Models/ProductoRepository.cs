using Dapper;
using Microsoft.Data.SqlClient;

namespace Quantitary.Models;

public class VentaRegistro
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public string NombreProducto { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string MetodoPago { get; set; } = string.Empty;
    public int Cantidad { get; set; } = 1;
    public decimal Precio { get; set; }
    public decimal Costo { get; set; }
}

public class ProductoRanking
{
    public string Nombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal Total { get; set; }
}

public class ChartPoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Valor { get; set; }
}

public class DashboardCategoryShare
{
    public string Nombre { get; set; } = string.Empty;
    public decimal Valor { get; set; }
}

public class DashboardSummary
{
    public int TotalProductos { get; set; }
    public int ProductosPocoStock { get; set; }
    public int ProductosSinStock { get; set; }
    public decimal GananciasPorMesNetas { get; set; }
    public decimal GananciaPromedioPorProducto { get; set; }
    public int VentasTotales { get; set; }
    public string MesMayorVentas { get; set; } = string.Empty;
    public decimal VentaPromedioPorMes { get; set; }
    public List<ProductoRanking> PodioProductosMasVendidos { get; set; } = new();
    public List<ProductoRanking> PodioProductosMasGanancia { get; set; } = new();
    public List<VentaRegistro> UltimosProductosVendidos { get; set; } = new();
    public List<string> BasesDeDatos { get; set; } = new();
    public decimal TotalVentasMes { get; set; }
    public decimal GananciaNetaMes { get; set; }
    public decimal GastosTotalesMes { get; set; }
    public decimal VentaPromedioMes { get; set; }
    public string MesActualLabel { get; set; } = string.Empty;
    public List<ChartPoint> VentasPorPeriodo { get; set; } = new();
    public List<ChartPoint> GananciaPorCategoria { get; set; } = new();
    public List<DashboardCategoryShare> ParticipacionVentas { get; set; } = new();
}

public class ProductoIndicadorRow
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public int StockActual { get; set; }
    public int StockMinimo { get; set; }
    public int StockMinimoRecomendado { get; set; }
    public string MinimoReposicionTexto { get; set; } = "Mantener";
    public decimal Precio { get; set; }
    public decimal CostoCompra { get; set; }
    public int CantidadVendida { get; set; }
    public decimal VentasTotales { get; set; }
    public decimal GananciaTotal { get; set; }
    public decimal Rotacion { get; set; }
    public decimal Margen { get; set; }
    public string AccionSugerida { get; set; } = "Mantener";
    public string RecomendacionAbastecimiento { get; set; } = "Sin acción";
    public string FechaCompraSugerida { get; set; } = "Próximos 5 días";
    public string CompraPlazoTexto { get; set; } = "Dentro de 7 días";
    public string FechaVentaSugerida { get; set; } = "Próximos 7 días";
    public string AjustePrecio { get; set; } = "Sin cambio";
    public string DiaMasVendido { get; set; } = "Sin datos";
    public string DiaMenosVendido { get; set; } = "Sin datos";
    public string FestividadesSugeridas { get; set; } = "Sin datos";
    public string RecomendacionTexto { get; set; } = string.Empty;
    public string SiguientePaso { get; set; } = string.Empty;
}

public class IndicadoresYOportunidadesViewModel
{
    public decimal MargenNetoGeneral { get; set; }
    public string ProductoMayorRotacion { get; set; } = "Sin datos";
    public string RelacionInteresCompra { get; set; } = "Sin datos";
    public string CategoriaMasVentas { get; set; } = "Sin datos";
    public int TotalProductos { get; set; }
    public string ResumenEstado { get; set; } = "Sin alertas";
    public string MensajePersonalizadoProximoPaso { get; set; } = string.Empty;
    public List<string> ProductosReponer { get; set; } = new();
    public List<string> ProductosComprarMenos { get; set; } = new();
    public List<string> ProductosComprarMas { get; set; } = new();
    public List<string> ProductosCambiarMinimo { get; set; } = new();
    public List<string> RecomendacionesTexto { get; set; } = new();
    public List<string> ExplicacionesIndicadores { get; set; } = new();
    public List<ProductoIndicadorRow> Productos { get; set; } = new();
}

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

    public IndicadoresYOportunidadesViewModel ObtenerIndicadoresYOportunidades(int? empresaId = null)
    {
        using var connection = BD.CrearConexion(_configuration);

        var productos = connection.Query<ProductoIndicadorBase>(@"
            SELECT p.id AS Id,
                   p.nombre AS Nombre,
                   p.categoria AS Categoria,
                   p.precio AS Precio,
                   p.stockActual AS StockActual,
                   p.stockMinimo AS StockMinimo,
                   p.costoCompra AS CostoCompra,
                   p.empresaID AS EmpresaId
            FROM Productos p
            WHERE (@EmpresaId IS NULL OR p.empresaID = @EmpresaId)
            ORDER BY p.nombre",
            new { EmpresaId = empresaId == 0 ? null : empresaId }).ToList();

        var ventasPorProducto = connection.Query<ProductoVentaAggregate>(@"
            SELECT v.productoId AS ProductoId,
                   SUM(v.cantidad) AS CantidadVendida,
                   SUM(v.precio) AS VentasTotales,
                   SUM(v.costo) AS CostoTotal
            FROM Ventas v
            INNER JOIN Productos p ON p.id = v.productoId
            WHERE (@EmpresaId IS NULL OR p.empresaID = @EmpresaId)
            GROUP BY v.productoId",
            new { EmpresaId = empresaId == 0 ? null : empresaId }).ToList();

        var ventasPorCategoria = connection.Query<ProductoCategoriaAggregate>(@"
            SELECT p.categoria AS Categoria,
                   SUM(v.precio) AS VentasTotales
            FROM Ventas v
            INNER JOIN Productos p ON p.id = v.productoId
            WHERE (@EmpresaId IS NULL OR p.empresaID = @EmpresaId)
            GROUP BY p.categoria",
            new { EmpresaId = empresaId == 0 ? null : empresaId }).ToList();

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

        var margenNetoGeneral = filas.Any() && filas.Sum(x => x.VentasTotales) > 0
            ? (filas.Sum(x => x.GananciaTotal) / filas.Sum(x => x.VentasTotales)) * 100m
            : 0m;

        var topRotacion = filas.OrderByDescending(x => x.Rotacion).FirstOrDefault();
        var categoriaTop = ventasPorCategoria.OrderByDescending(x => x.VentasTotales).FirstOrDefault();
        var interesCompra = filas.Any() ? Math.Round(filas.Max(x => x.Rotacion * 100m), 1) : 0m;
        var mensajePersonalizado = filas.Any()
            ? $"El próximo paso para vender más es reforzar el stock de {string.Join(", ", filas.Where(x => x.StockActual <= x.StockMinimo || x.AccionSugerida == "Reponer").Take(3).Select(x => x.Nombre)) ?? "los productos más vendidos"} y poner foco desde {ObtenerDiaMasVendido("General")} para evitar quedarte sin stock cuando llega la demanda."
            : "El próximo paso para vender más es revisar el stock y reforzar las promociones por día de la semana.";

        var model = new IndicadoresYOportunidadesViewModel
        {
            MargenNetoGeneral = margenNetoGeneral,
            ProductoMayorRotacion = topRotacion?.Nombre ?? "Sin datos",
            RelacionInteresCompra = filas.Any() ? $"{interesCompra}%" : "Sin datos",
            CategoriaMasVentas = categoriaTop?.Categoria ?? "Sin datos",
            TotalProductos = filas.Count,
            ResumenEstado = filas.Any(x => x.AccionSugerida == "Reponer") ? "Hay productos con reposición urgente" : "Todo en línea",
            MensajePersonalizadoProximoPaso = mensajePersonalizado,
            ProductosReponer = filas
                .Where(x => x.StockActual <= x.StockMinimo)
                .Select(x => x.Nombre)
                .Take(5)
                .ToList(),
            ProductosComprarMenos = filas
                .Where(x => x.StockActual > x.StockMinimo * 2 && x.CantidadVendida == 0)
                .Select(x => x.Nombre)
                .Take(5)
                .ToList(),
            ProductosComprarMas = filas
                .Where(x => x.StockActual <= x.StockMinimo || (x.CantidadVendida > 0 && x.StockActual < x.StockMinimo * 2))
                .Select(x => x.Nombre)
                .Take(5)
                .ToList(),
            ProductosCambiarMinimo = filas
                .Where(x => x.StockActual > x.StockMinimo * 2 && x.CantidadVendida > 0)
                .Select(x => x.Nombre)
                .Take(5)
                .ToList(),
            RecomendacionesTexto = filas
                .Select(x => x.RecomendacionTexto)
                .Take(6)
                .ToList(),
            ExplicacionesIndicadores = new List<string>
            {
                "Margen neto general: cuánto ganas por cada peso vendido tras cubrir costos.",
                "Rotación: qué tan rápido se mueve cada producto frente a su stock.",
                "Día más vendido: el mejor momento para vender y promocionar.",
                "Día menos vendido: el momento para ajustar precio o stock.",
                "Mínimo de reposición: indica si hay que subirlo o bajarlo para comprar en el momento correcto."
            },
            Productos = filas
                .OrderByDescending(x => x.Rotacion)
                .ThenBy(x => x.Nombre)
                .Take(12)
                .ToList()
        };

        return model;
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

    private sealed class ProductoIndicadorBase
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
        public decimal CostoCompra { get; set; }
        public int EmpresaId { get; set; }
    }

    private sealed class ProductoVentaAggregate
    {
        public int ProductoId { get; set; }
        public int CantidadVendida { get; set; }
        public decimal VentasTotales { get; set; }
        public decimal CostoTotal { get; set; }
    }

    private sealed class ProductoCategoriaAggregate
    {
        public string Categoria { get; set; } = string.Empty;
        public decimal VentasTotales { get; set; }
    }
}
