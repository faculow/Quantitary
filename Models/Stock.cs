namespace Quantitary.Models
{
    public class Stock
    {
        public int Id { get; set; }
        public int ProductoID { get; set; }
        public int Cantidad { get; set; }
        public int CantidadMinimoAlerta { get; set; }
        public bool Activo { get; set; } = true;
        public DateTime ActualizadoEn { get; set; } = DateTime.Now;

        public Productos? Producto { get; set; }
    }
}