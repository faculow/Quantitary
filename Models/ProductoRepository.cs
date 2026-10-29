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

    public bool TieneProductos(int empresaId)
        => BD.TieneProductos(_configuration, empresaId);

    public IConfiguration GetConfiguration()
        => _configuration;
}
