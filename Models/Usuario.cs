namespace Quantitary.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
        public string Contrasena { get; set; } = string.Empty;
        public string Mail { get; set; } = string.Empty;
        public string? NumeroTelefono { get; set; }
        public int EmpresaID { get; set; }
        public int RolID { get; set; }

        public Empresa? Empresa { get; set; }
        public Rol? Rol { get; set; }
        public List<Venta> Ventas { get; set; } = new List<Venta>();
    }
}