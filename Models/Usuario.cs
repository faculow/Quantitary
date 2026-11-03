using System.ComponentModel.DataAnnotations;

namespace Quantitary.Models
{
    public class Usuario
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre de usuario debe tener entre 3 y 100 caracteres.")]
        [RegularExpression(@"^(?=.{3,100}$)(?!\s)[A-Za-z0-9._-]+$", ErrorMessage = "El nombre de usuario solo puede contener letras, números, puntos, guiones y guion bajo.")]
        public string NombreUsuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [StringLength(255, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)[A-Za-z\d!@#$%^&*()_+\-\[\]{};':""\\|,.<>/?]{6,255}$", ErrorMessage = "La contraseña debe incluir mayúsculas, minúsculas y números.")]
        public string Contrasena { get; set; } = string.Empty;

        [Required(ErrorMessage = "El email es obligatorio.")]
        [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
        public string Mail { get; set; } = string.Empty;

        [Phone(ErrorMessage = "El número de teléfono no tiene un formato válido.")]
        public string? NumeroTelefono { get; set; }

        [Required(ErrorMessage = "El nombre de la empresa es obligatorio.")]
        [StringLength(200, MinimumLength = 2, ErrorMessage = "El nombre de la empresa debe tener entre 2 y 200 caracteres.")]
        public string? NombreEmpresa { get; set; } = string.Empty;

        public int EmpresaID { get; set; }
        public int RolID { get; set; }

        public Empresa? Empresa { get; set; }
        public Rol? Rol { get; set; }
        public List<Venta> Ventas { get; set; } = new List<Venta>();
    }
}