namespace Quantitary.Models;

public sealed class ReporteGuardado
{
    public int Id { get; set; }
    public int UsuarioID { get; set; }
    public int EmpresaID { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string TipoReporte { get; set; } = string.Empty;
    public string PeriodoTiempo { get; set; } = string.Empty;
    public string FormatoReporte { get; set; } = string.Empty;
    public string FrecuenciaActualizacion { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public string VistaPreviaHtml { get; set; } = string.Empty;
    public decimal TotalVentas { get; set; }
    public decimal Ganancia { get; set; }
    public decimal Gastos { get; set; }
    public decimal VentaPromedio { get; set; }
    public int CantidadProductosVendidos { get; set; }
    public int TotalProductos { get; set; }
    public int ProductosPocoStock { get; set; }
    public int ProductosSinStock { get; set; }
    public decimal MargenNeto { get; set; }
    public int StockSobra { get; set; }
    public string Hallazgos { get; set; } = string.Empty;
    public string Recomendaciones { get; set; } = string.Empty;
    public List<int> ProductosIds { get; set; } = new List<int>();
}
