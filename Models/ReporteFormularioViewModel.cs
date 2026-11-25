using System.ComponentModel.DataAnnotations;

namespace Quantitary.Models;

public sealed class ReporteFormularioViewModel
{
    [Required(ErrorMessage = "El nombre del reporte es obligatorio.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 120 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Seleccioná el tipo de reporte.")]
    public string TipoReporte { get; set; } = "Ventas";

    [Required(ErrorMessage = "Seleccioná el período de tiempo.")]
    public string PeriodoTiempo { get; set; } = "Mensual";

    [Required(ErrorMessage = "Seleccioná el formato del reporte.")]
    public string FormatoReporte { get; set; } = "PDF";

    [Required(ErrorMessage = "Indicá cada cuánto se actualiza.")]
    public string FrecuenciaActualizacion { get; set; } = "Diario";

    public List<int> ProductosIds { get; set; } = new List<int>();
}
