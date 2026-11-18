namespace Quantitary.Models;

public sealed class VentaDiaria
{
    public DateTime Fecha { get; set; }
    public int Operaciones { get; set; }
    public int Unidades { get; set; }
    public decimal Total { get; set; }
    public decimal Costo { get; set; }
}
