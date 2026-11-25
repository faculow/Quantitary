using System.ComponentModel.DataAnnotations;

namespace Quantitary.Models;

public sealed class PerfilAjustesInput
{
    [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres.")]
    [RegularExpression(@"^[A-Za-zÁÉÍÓÚÜÑáéíóúüñ0-9._-]+$", ErrorMessage = "Usá solamente letras, números, puntos, guiones o guion bajo.")]
    public string NombreUsuario { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingresá un correo válido.")]
    [StringLength(150)]
    public string Mail { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Ingresá un teléfono válido.")]
    [StringLength(30)]
    public string? NumeroTelefono { get; set; }
}
