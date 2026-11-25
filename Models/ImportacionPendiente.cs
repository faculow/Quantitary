namespace Quantitary.Models;

public class ImportacionPendiente
{
    public string NombreArchivo { get; set; } = string.Empty;
    public List<Producto> Productos { get; set; } = new();
    public List<ProductoImportacionPreview> Conflictos { get; set; } = new();
    public List<ProductoNombreConflicto> ConflictosPorNombre { get; set; } = new();
}
