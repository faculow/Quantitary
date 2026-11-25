namespace Quantitary.Models;

public class ProductoIndicadorRow
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public int StockActual { get; set; }
    public int StockMinimo { get; set; }
    public int StockMinimoRecomendado { get; set; }
    public string MinimoReposicionTexto { get; set; } = "Mantener";
    public decimal Precio { get; set; }
    public decimal CostoCompra { get; set; }
    public int CantidadVendida { get; set; }
    public decimal VentasTotales { get; set; }
    public decimal GananciaTotal { get; set; }
    public decimal Rotacion { get; set; }
    public decimal Margen { get; set; }
    public string AccionSugerida { get; set; } = "Mantener";
    public string RecomendacionAbastecimiento { get; set; } = "Sin acción";
    public string FechaCompraSugerida { get; set; } = "Próximos 5 días";
    public string CompraPlazoTexto { get; set; } = "Dentro de 7 días";
    public string FechaVentaSugerida { get; set; } = "Próximos 7 días";
    public string AjustePrecio { get; set; } = "Sin cambio";
    public string DiaMasVendido { get; set; } = "Sin datos";
    public string DiaMenosVendido { get; set; } = "Sin datos";
    public string FestividadesSugeridas { get; set; } = "Sin datos";
    public string RecomendacionTexto { get; set; } = string.Empty;
    public string SiguientePaso { get; set; } = string.Empty;
}
