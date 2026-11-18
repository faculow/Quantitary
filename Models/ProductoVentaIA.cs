namespace Quantitary.Models;

public sealed class ProductoVentaIA
{
    public string Nombre { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int Unidades { get; set; }
    public decimal Facturacion { get; set; }
    public decimal Ganancia { get; set; }
}
