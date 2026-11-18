namespace Quantitary.Models;

public sealed class AnalisisProductoData
{
    public List<AnalisisProductoRow> Productos { get; set; } = new List<AnalisisProductoRow>();
    public List<AnalisisVentaProductoRow> VentasPorProducto { get; set; } = new List<AnalisisVentaProductoRow>();
    public List<dynamic> VentasPorMes { get; set; } = new List<dynamic>();
    public List<dynamic> InteresCompraPorMes { get; set; } = new List<dynamic>();
    public List<dynamic> VentasSemana { get; set; } = new List<dynamic>();
}
