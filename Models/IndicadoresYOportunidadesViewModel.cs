namespace Quantitary.Models;

public class IndicadoresYOportunidadesViewModel
{
    public decimal MargenNetoGeneral { get; set; }
    public string ProductoMayorRotacion { get; set; } = "Sin datos";
    public string RelacionInteresCompra { get; set; } = "Sin datos";
    public string CategoriaMasVentas { get; set; } = "Sin datos";
    public int TotalProductos { get; set; }
    public string ResumenEstado { get; set; } = "Sin alertas";
    public string MensajePersonalizadoProximoPaso { get; set; } = string.Empty;
    public List<string> ProductosReponer { get; set; } = new();
    public List<string> ProductosComprarMenos { get; set; } = new();
    public List<string> ProductosComprarMas { get; set; } = new();
    public List<string> ProductosCambiarMinimo { get; set; } = new();
    public List<string> RecomendacionesTexto { get; set; } = new();
    public List<string> ExplicacionesIndicadores { get; set; } = new();
    public List<ProductoIndicadorRow> Productos { get; set; } = new();
}
