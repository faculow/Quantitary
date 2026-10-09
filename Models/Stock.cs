namespace Quantitary.Models
{
    public class Stock
    {
        public int Id { get; set; }
        public int ProductoID { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal RotacionMensual { get; set; }
        public int CantidadMinimoAlerta { get; set; }
        public bool Activo { get; set; }

        public Productos? Producto { get; set; }
    }
}