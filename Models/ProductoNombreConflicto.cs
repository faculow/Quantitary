namespace Quantitary.Models;

public class ProductoNombreConflicto
{
    public string Sku { get; set; } = string.Empty;
    public string NombreImportado { get; set; } = string.Empty;
    public string NombreActual { get; set; } = string.Empty;
    public string NombreSugerido { get; set; } = string.Empty;
    public List<ProductoCoincidente> ProductosCoincidentes { get; set; } = new();
}
