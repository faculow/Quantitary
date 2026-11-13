namespace Quantitary.Models
{
    public class StockRegistro
    {
        public int ProductoId { get; set; }
        public string NombreProducto { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public string Proveedor { get; set; } = string.Empty;
    }
}
