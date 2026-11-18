namespace Quantitary.Models;

public sealed class AnalisisVentasGananciasData
{
    public decimal VentasTotales { get; set; }
    public decimal GastosTotales { get; set; }
    public decimal VentaPromedio { get; set; }
    public decimal Ganancias { get; set; }
    public int VentasTotalesCantidad { get; set; }
    public List<ChartPoint> VentasDiarias { get; set; } = new List<ChartPoint>();
    public List<ChartPoint> VentaPorSemana { get; set; } = new List<ChartPoint>();
    public List<ChartPoint> GananciaPorCategoria { get; set; } = new List<ChartPoint>();
    public decimal MargenNeto { get; set; }
    public decimal FormulaGanancia { get; set; }
}
