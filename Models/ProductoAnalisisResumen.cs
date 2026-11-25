namespace Quantitary.Models;

public sealed class ProductoAnalisisResumen
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public decimal Costo { get; set; }
    public decimal Gasto { get; set; }
    public decimal Ganancia { get; set; }
    public decimal Margen { get; set; }
    public int Stock { get; set; }
    public int StockMinimo { get; set; }
    public string Reposicion { get; set; } = string.Empty;
    public decimal Rotacion { get; set; }
    public int VentasMes { get; set; }
    public int InteresCompraMes { get; set; }
}
