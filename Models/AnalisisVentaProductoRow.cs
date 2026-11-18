namespace Quantitary.Models;

public sealed class AnalisisVentaProductoRow
{
    public int ProductoId { get; set; }
    public int CantidadVendida { get; set; }
    public decimal VentasTotales { get; set; }
    public decimal GastoTotal { get; set; }
    public decimal GananciaTotal { get; set; }
}
