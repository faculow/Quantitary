using Quantitary.Services;

namespace Quantitary.Models;

public sealed class ProductoAnalisisRow
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public decimal CostoCompra { get; set; }
    public int StockActual { get; set; }
    public int StockMinimo { get; set; }
    public decimal GastoTotal { get; set; }
    public decimal GananciaTotal { get; set; }
    public int CantidadVendida { get; set; }
    public decimal VentasTotales { get; set; }
}

public sealed class ProductoAnalisisResumen
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public decimal Costo { get; set; }
    public decimal Gasto { get; set; }
    public decimal Ganancia { get; set; }
    public decimal Margen { get; set; }
    public int Stock { get; set; }
    public int StockMinimo { get; set; }
    public string Reposicion { get; set; } = string.Empty;
    public decimal Rotacion { get; set; }
    public int VentasMes { get; set; }
    public int InteresCompraMes { get; set; }
}

public sealed class AjustesPerfilDatos
{
    public string NombreUsuario { get; init; } = string.Empty;
    public string Mail { get; init; } = string.Empty;
    public string? NumeroTelefono { get; init; }
    public string NombreEmpresa { get; init; } = string.Empty;
    public bool PuedeEditarEmpresa { get; init; }
}

public sealed class AjustesRepetidos
{
    public int Mail { get; init; }
    public int Nombre { get; init; }
}

public sealed record ChatRenombrarRequest(string? Titulo);

public sealed record ChatConfirmacionRequest(string? Token);

public sealed record IAOperacionPendiente(
    string Token,
    IAProductoSql.Solicitud Solicitud,
    int Usuario,
    int Empresa,
    int Paso,
    DateTime Vence,
    string Vista);
