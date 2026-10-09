namespace Quantitary.Models
{
    public class Permisos
    {
        public int Id { get; set; }
        public bool RegistrarVentas { get; set; }
        public bool VerHistorialVentas { get; set; }
        public bool AnularVentas { get; set; }
        public bool VerProductos { get; set; }
        public bool CrearProductos { get; set; }
        public bool EditarProductos { get; set; }
        public bool EliminarProductos { get; set; }
        public bool VerStock { get; set; }
        public bool VerDashboard { get; set; }
        public bool GestionarUsuarios { get; set; }
        public bool VerGanacia { get; set; }

        public List<Rol> Roles { get; set; } = new List<Rol>();
    }
}