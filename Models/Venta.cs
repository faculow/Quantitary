namespace Quantitary.Models
{
    public class Venta
    {
        public int Id { get; set; }
        public int CantidadDeVentas { get; set; }
        public int UsuarioID { get; set; }
        public int ProductoID { get; set; }

        public Usuarios? Usuario { get; set; }
        public Productos? Producto { get; set; }
        public List<DetalleVenta> DetalleVentas { get; set; } = new List<DetalleVenta>();
    }
}