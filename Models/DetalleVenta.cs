namespace Quantitary.Models
{
    public class DetalleVenta
    {
        public int Id { get; set; }
        public int VentaID { get; set; }
        public int ProductoID { get; set; }
        public decimal PrecioVenta { get; set; }
        public decimal PrecioCompra { get; set; }
        public decimal? PorcentajeGanancia { get; set; }

        public Venta? Venta { get; set; }
        public Productos? Producto { get; set; }
    }
}