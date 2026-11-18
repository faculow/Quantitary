namespace Quantitary.Models;

public sealed class ProductoVentaAggregate
{
    public int ProductoId { get; set; }
    public int CantidadVendida { get; set; }
    public decimal VentasTotales { get; set; }
    public decimal CostoTotal { get; set; }
}
