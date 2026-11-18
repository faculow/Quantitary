namespace Quantitary.Models;

public sealed class AlertaStockRegistro
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int Stock { get; set; }
    public int Minimo { get; set; }
}
