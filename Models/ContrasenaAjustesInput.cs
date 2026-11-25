using System.ComponentModel.DataAnnotations;

namespace Quantitary.Models;

public sealed class ContrasenaAjustesInput
{
    [Required(ErrorMessage = "Ingresá tu contraseña actual.")]
    public string ContrasenaActual { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresá la nueva contraseña.")]
    [StringLength(255, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    public string ContrasenaNueva { get; set; } = string.Empty;

    [Required(ErrorMessage = "Repetí la nueva contraseña.")]
    [Compare(nameof(ContrasenaNueva), ErrorMessage = "Las contraseñas nuevas no coinciden.")]
    public string Confirmacion { get; set; } = string.Empty;
}
