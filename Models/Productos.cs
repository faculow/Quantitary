namespace Quantitary.Models
{
    public class Productos
    {
        public int Id { get; set; }
        public decimal Precio { get; set; }
        public int CategoriaID { get; set; }
        public decimal? ValorVenta { get; set; }
        public int ProveedorID { get; set; }
        public int CantidadDeVentas { get; set; }

        public Categoria? Categoria { get; set; }
        public Proveedores? Proveedor { get; set; }
        public List<Stock> Stocks { get; set; } = new List<Stock>();
        public List<Venta> Ventas { get; set; } = new List<Venta>();
        public List<DetalleVenta> DetalleVentas { get; set; } = new List<DetalleVenta>();
    }
}