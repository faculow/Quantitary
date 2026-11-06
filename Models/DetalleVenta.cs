namespace Quantitary.Models
{
    public class DetalleVenta
    {
        public int Id { get; set; }
        public int VentaID { get; set; }
        public int ProductoID { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioVenta { get; set; }
        public decimal CostoCompra { get; set; }
        public decimal? PorcentajeGanancia { get; set; }
        public decimal Ganancia { get; set; }

        public Venta? Venta { get; set; }
        public Productos? Producto { get; set; }
    }
}