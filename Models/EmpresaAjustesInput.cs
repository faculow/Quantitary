using System.ComponentModel.DataAnnotations;

namespace Quantitary.Models;

public sealed class EmpresaAjustesInput
{
    [Required(ErrorMessage = "El nombre de la empresa es obligatorio.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;
}
