namespace Quantitary.Models
{
    public class Proveedor
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int CantidadProductos { get; set; }
    }

    public class Proveedores
    {
        public int Id { get; set; }
        public string? ProductoPrincipal { get; set; }
        public decimal CostoEnvio { get; set; }
        public string? Detalles { get; set; }

        public List<Productos> Productos { get; set; } = new List<Productos>();
    }
}