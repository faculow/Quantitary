namespace Quantitary.Models
{
    public class Empresa
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;

        public List<Usuario> Usuarios { get; set; } = new List<Usuario>();
    }
}