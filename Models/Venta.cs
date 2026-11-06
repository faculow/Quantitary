namespace Quantitary.Models
{
    public class Venta
    {
        public int Id { get; set; }
        public int EmpresaID { get; set; }
        public int UsuarioID { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
        public string MetodoPago { get; set; } = "Efectivo";
        public decimal TotalVenta { get; set; }
        public string? Observacion { get; set; }

        public Usuario? Usuario { get; set; }
        public List<DetalleVenta> DetalleVentas { get; set; } = new List<DetalleVenta>();
    }
}