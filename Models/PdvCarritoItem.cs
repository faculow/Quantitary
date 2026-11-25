namespace Quantitary.Models;

public class PdvCarritoItem
{
    public int ProductoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Marca { get; set; } = string.Empty;
    public decimal PrecioUnitario { get; set; }
    public int StockActual { get; set; }
    public int Cantidad { get; set; } = 1;
}
