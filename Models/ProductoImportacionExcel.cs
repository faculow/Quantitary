using Microsoft.AspNetCore.Http;

namespace Quantitary.Models;

public class ProductoImportacionExcel
{
    public IFormFile? Archivo { get; set; }

    public static readonly string[] ColumnasEsperadas =
    {
        "Nombre", "SKU", "Categoria", "PrecioCompra", "PrecioVenta", "Proveedor"
    };

    public static readonly string[] ColumnasOpcionales =
    {
        "StockInicial", "StockMinimo"
    };

    public static readonly Dictionary<string, string[]> AliasColumnas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Nombre"] = ["Nombre", "nombre", "Producto", "DescripcionCorta", "Producto Nombre"],
        ["SKU"] = ["SKU", "Sku", "sku", "Codigo", "Cod", "Código", "Codigo Producto", "CodigoProducto"],
        ["Categoria"] = ["Categoria", "categoria", "Categoría", "Rubro", "Categoria Producto"],
        ["PrecioCompra"] = ["PrecioCompra", "preciocompra", "Costo", "costo", "CostoCompra", "costoCompra", "Precio compra", "Precio de compra", "Costo unitario"],
        ["PrecioVenta"] = ["PrecioVenta", "precioventa", "Precio", "precio", "PrecioUnitario", "Valor", "Precio unitario", "Precio de venta"],
        ["Proveedor"] = ["Proveedor", "proveedor", "Fabricante", "Marca", "Proveedor Nombre"],
        ["StockInicial"] = ["StockInicial", "stockInicial", "StockActual", "stockActual", "Stock", "CantidadInicial", "cantidadInicial", "InventarioInicial", "inventarioInicial", "Stock actual", "Cantidad inicial", "Stock Actual", "Cantidad Inicial"],
        ["StockMinimo"] = ["StockMinimo", "stockMinimo", "Minimo", "minimo", "CantidadMinima", "cantidadMinima", "InventarioMinimo", "inventarioMinimo", "Stock mínimo", "Stock Minimo", "Cantidad mínima", "Cantidad Minima", "StockMinimo"]
    };
}
