namespace Quantitary.Models
{
    public class Rol
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int PermisoID { get; set; }

        public Permisos? Permiso { get; set; }
        public List<Usuario> Usuarios { get; set; } = new List<Usuario>();
    }
}