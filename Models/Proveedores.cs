namespace Quantitary.Models
{
    public class Proveedores
    {
        public int Id { get; set; }
        public string? ProductoPrincipal { get; set; }
        public decimal CostoEnvio { get; set; }
        public string? Detalles { get; set; }

        public List<Productos> Productos { get; set; } = new List<Productos>();
    }
}