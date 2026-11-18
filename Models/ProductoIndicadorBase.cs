namespace Quantitary.Models;

public sealed class ProductoIndicadorBase
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
