namespace Quantitary.Models;

public sealed class ProductoAnalisisRow
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
