using System.ComponentModel.DataAnnotations;

namespace Quantitary.Models
{
    public class Usuario
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre de usuario debe tener entre 3 y 100 caracteres.")]
        public string NombreUsuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [StringLength(255, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        public string Contrasena { get; set; } = string.Empty;

        [Required(ErrorMessage = "El email es obligatorio.")]
        [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
        public string Mail { get; set; } = string.Empty;

        [Phone(ErrorMessage = "El número de teléfono no tiene un formato válido.")]
        public string? NumeroTelefono { get; set; }

        public string? NombreEmpresa { get; set; }
        public int EmpresaID { get; set; }
        public int RolID { get; set; }

        public Empresa? Empresa { get; set; }
        public Rol? Rol { get; set; }
        public List<Venta> Ventas { get; set; } = new List<Venta>();
    }
}