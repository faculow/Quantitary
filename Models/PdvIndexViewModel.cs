namespace Quantitary.Models;

public class PdvIndexViewModel
{
    public List<Producto> Productos { get; set; } = new();
    public List<PdvCarritoItem> Carrito { get; set; } = new();
    public List<string> Categorias { get; set; } = new();
    public string? Filtro { get; set; }
    public string? Categoria { get; set; }
    public decimal TotalCarrito => Carrito.Sum(item => item.PrecioUnitario * item.Cantidad);
}
