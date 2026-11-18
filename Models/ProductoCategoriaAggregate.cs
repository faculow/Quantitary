namespace Quantitary.Models;

public sealed class ProductoCategoriaAggregate
{
    public string Categoria { get; set; } = string.Empty;
    public decimal VentasTotales { get; set; }
}
