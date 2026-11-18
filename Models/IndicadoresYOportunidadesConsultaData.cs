namespace Quantitary.Models;

public sealed class IndicadoresYOportunidadesConsultaData
{
    public List<ProductoIndicadorBase> Productos { get; set; } = new List<ProductoIndicadorBase>();
    public List<ProductoVentaAggregate> VentasPorProducto { get; set; } = new List<ProductoVentaAggregate>();
    public List<ProductoCategoriaAggregate> VentasPorCategoria { get; set; } = new List<ProductoCategoriaAggregate>();
}
