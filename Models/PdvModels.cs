namespace Quantitary.Models;

public class PdvCarritoItem
{
    public int ProductoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Marca { get; set; } = string.Empty;
    public decimal PrecioUnitario { get; set; }
    public int StockActual { get; set; }
    public int Cantidad { get; set; } = 1;
}

public class PdvIndexViewModel
{
    public List<Producto> Productos { get; set; } = new();
    public List<PdvCarritoItem> Carrito { get; set; } = new();
    public List<string> Categorias { get; set; } = new();
    public string? Filtro { get; set; }
    public string? Categoria { get; set; }
    public decimal TotalCarrito => Carrito.Sum(item => item.PrecioUnitario * item.Cantidad);
}

public class PdvVentaRequest
{
    public string MetodoPago { get; set; } = "Efectivo";
}
