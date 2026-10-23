using Dapper;
using Microsoft.Data.SqlClient;

namespace Quantitary.Models;

public class Producto
{
    public int Id { get; set; }
    public int EmpresaID { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; } = DateTime.Now;
    public decimal Precio { get; set; }
    public int StockActual { get; set; }
    public int StockMinimo { get; set; }
    public string Proveedor { get; set; } = string.Empty;
    public decimal CostoCompra { get; set; }
    public decimal? PrecioVenta { get; set; }
    public int CantidadVentas { get; set; }
}