using System.ComponentModel.DataAnnotations;

namespace Quantitary.Models;

public sealed class AjustesViewModel
{
    public PerfilAjustesInput Perfil { get; set; } = new();
    public EmpresaAjustesInput Empresa { get; set; } = new();
    public ContrasenaAjustesInput Seguridad { get; set; } = new();
    public bool PuedeEditarEmpresa { get; set; }
    public string SeccionActiva { get; set; } = "perfil";
}

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

public sealed class EmpresaAjustesInput
{
    [Required(ErrorMessage = "El nombre de la empresa es obligatorio.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;
}

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
