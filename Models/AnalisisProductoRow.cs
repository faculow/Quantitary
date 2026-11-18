namespace Quantitary.Models;

public sealed class AnalisisProductoRow
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public decimal CostoCompra { get; set; }
    public int StockActual { get; set; }
    public int StockMinimo { get; set; }
    public int CantidadVendida { get; set; }
}
