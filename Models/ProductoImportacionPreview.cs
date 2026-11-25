namespace Quantitary.Models;

public class ProductoImportacionPreview
{
    public string Sku { get; set; } = string.Empty;
    public string NombreActual { get; set; } = string.Empty;
    public string NombreNuevo { get; set; } = string.Empty;
    public string CategoriaActual { get; set; } = string.Empty;
    public string CategoriaNueva { get; set; } = string.Empty;
    public string ProveedorActual { get; set; } = string.Empty;
    public string ProveedorNuevo { get; set; } = string.Empty;
    public int StockActual { get; set; }
    public int StockNuevo { get; set; }
    public decimal PrecioCompraActual { get; set; }
    public decimal PrecioCompraNuevo { get; set; }
    public decimal PrecioVentaActual { get; set; }
    public decimal PrecioVentaNuevo { get; set; }
    public string Tipo { get; set; } = "duplicado";
}
