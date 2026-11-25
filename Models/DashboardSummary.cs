namespace Quantitary.Models;

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
