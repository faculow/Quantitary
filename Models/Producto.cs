using System.ComponentModel.DataAnnotations;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Quantitary.Models; 

public class Producto
{
    public int Id { get; set; }
    public int EmpresaID { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El SKU es obligatorio.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El SKU debe tener entre 2 y 100 caracteres.")]
    public string Sku { get; set; } = string.Empty;

    [Required(ErrorMessage = "La categoría es obligatoria.")]
    public string Categoria { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public DateTime FechaCarga { get; set; } = DateTime.Now;

    [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor a cero.")]
    public decimal Precio { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "El stock no puede ser negativo.")]
    public int StockActual { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "El stock mínimo no puede ser negativo.")]
    public int StockMinimo { get; set; }

    public string Proveedor { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "El costo no puede ser negativo.")]
    public decimal CostoCompra { get; set; }
    public decimal? PrecioVenta { get; set; }
    public int CantidadVentas { get; set; }

    public string Marca { get; set; } = string.Empty;
    public decimal? Peso { get; set; }
    public string UnidadMedida { get; set; } = "kg";
}