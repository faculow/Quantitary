using System.Data;
using System.Globalization;
using System.Text;
using ExcelDataReader;
using Microsoft.AspNetCore.Http;

namespace Quantitary.Models;

public class ProductoImportacionExcel
{
    public IFormFile? Archivo { get; set; }

    public static readonly string[] ColumnasEsperadas =
    {
        "Nombre",
        "SKU",
        "Categoria",
        "Precio",
        "Proveedor"
    };

    public static readonly string[] ColumnasOpcionales =
    {
        "StockInicial",
        "StockMinimo"
    };

    public static readonly Dictionary<string, string[]> AliasColumnas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Nombre"] = ["Nombre", "nombre", "Producto", "DescripcionCorta", "Producto Nombre"],
        ["SKU"] = ["SKU", "Sku", "sku", "Codigo", "Cod", "Código", "Codigo Producto", "CodigoProducto"],
        ["Categoria"] = ["Categoria", "categoria", "Categoría", "Rubro", "Categoria Producto"],
        ["Precio"] = ["Precio", "precio", "PrecioUnitario", "Valor", "PrecioUnitario", "Precio Unitario"],
        ["Proveedor"] = ["Proveedor", "proveedor", "Fabricante", "Marca", "Proveedor Nombre"],
        ["StockInicial"] = ["StockInicial", "stockInicial", "StockActual", "stockActual", "Stock", "CantidadInicial", "cantidadInicial", "InventarioInicial", "inventarioInicial", "Stock actual", "Cantidad inicial", "Stock Actual", "Cantidad Inicial"],
        ["StockMinimo"] = ["StockMinimo", "stockMinimo", "Minimo", "minimo", "CantidadMinima", "cantidadMinima", "InventarioMinimo", "inventarioMinimo", "Stock mínimo", "Stock Minimo", "Cantidad mínima", "Cantidad Minima", "StockMinimo"]
    };
}

public static class ImportacionExcelHelper
{
    public static List<Producto> LeerProductos(Stream archivoStream)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var reader = ExcelReaderFactory.CreateReader(archivoStream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = true
            }
        });

        if (dataSet.Tables.Count == 0)
        {
            throw new InvalidOperationException("El archivo Excel está vacío o no tiene hojas de trabajo.");
        }

        var tabla = dataSet.Tables[0];
        var columnas = ObtenerColumnasRequeridas(tabla);

        var productos = new List<Producto>();

        foreach (DataRow fila in tabla.Rows)
        {
            if (fila == null || fila.ItemArray.All(valor => valor == null || string.IsNullOrWhiteSpace(valor.ToString())))
            {
                continue;
            }

            var producto = new Producto
            {
                Sku = ObtenerValor(fila, columnas, "SKU"),
                Nombre = ObtenerValor(fila, columnas, "Nombre"),
                Categoria = ObtenerValor(fila, columnas, "Categoria"),
                Proveedor = ObtenerValor(fila, columnas, "Proveedor"),
                Descripcion = "Importado desde Excel",
                FechaCarga = DateTime.Now,
                Precio = ObtenerDecimal(fila, columnas, "Precio"),
                StockActual = ObtenerEntero(fila, columnas, "StockInicial"),
                StockMinimo = ObtenerEntero(fila, columnas, "StockMinimo"),
                CostoCompra = 0m,
                PrecioVenta = 0m,
                CantidadVentas = 0
            };

            if (string.IsNullOrWhiteSpace(producto.Sku)
                || string.IsNullOrWhiteSpace(producto.Nombre)
                || string.IsNullOrWhiteSpace(producto.Categoria)
                || string.IsNullOrWhiteSpace(producto.Proveedor)
                || producto.Precio <= 0m)
            {
                continue;
            }

            productos.Add(producto);
        }

        if (productos.Count == 0)
        {
            throw new InvalidOperationException("No se encontraron filas válidas para importar. Revise los datos del Excel.");
        }

        return productos;
    }

    private static Dictionary<string, int> ObtenerColumnasRequeridas(DataTable tabla)
    {
        var columnas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (DataColumn columna in tabla.Columns)
        {
            columnas[NormalizarNombreColumna(columna.ColumnName)] = columna.Ordinal;
        }

        var faltantes = ProductoImportacionExcel.ColumnasEsperadas
            .Where(columnaEsperada => !ProductoImportacionExcel.AliasColumnas[columnaEsperada]
                .Any(alias => columnas.ContainsKey(NormalizarNombreColumna(alias))))
            .ToList();

        if (faltantes.Count > 0)
        {
            throw new InvalidOperationException($"Faltan columnas obligatorias en el archivo: {string.Join(", ", faltantes)}.");
        }

        return columnas;
    }

    private static string ObtenerValor(DataRow fila, Dictionary<string, int> columnas, string nombreEsperado)
    {
        foreach (var alias in ProductoImportacionExcel.AliasColumnas[nombreEsperado])
        {
            var claveNormalizada = NormalizarNombreColumna(alias);
            if (columnas.TryGetValue(claveNormalizada, out var indice))
            {
                var valor = fila[indice]?.ToString();
                return valor?.Trim() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static decimal ObtenerDecimal(DataRow fila, Dictionary<string, int> columnas, string nombreEsperado)
    {
        var valor = ObtenerValor(fila, columnas, nombreEsperado);
        if (string.IsNullOrWhiteSpace(valor))
        {
            return 0m;
        }

        return decimal.TryParse(valor.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator)
            .Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var resultado)
            ? resultado
            : 0m;
    }

    private static int ObtenerEntero(DataRow fila, Dictionary<string, int> columnas, string nombreEsperado)
    {
        var valor = ObtenerValor(fila, columnas, nombreEsperado);
        if (string.IsNullOrWhiteSpace(valor))
        {
            return 0;
        }

        return int.TryParse(valor, out var resultado) ? resultado : 0;
    }

    private static string NormalizarNombreColumna(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        var sinAcentos = new string(
            valor.Normalize(NormalizationForm.FormD)
                .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                .ToArray());

        var limpio = new string(sinAcentos.Where(ch => char.IsLetterOrDigit(ch)).ToArray());
        return limpio.ToLowerInvariant();
    }

    public static ImportacionPendiente AnalizarImportacion(List<Producto> productosImportados, List<Producto> productosExistentes)
    {
        var diccionarioExistentes = productosExistentes
            .GroupBy(p => (p.Sku ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var nombresExistentes = productosExistentes
            .Where(p => !string.IsNullOrWhiteSpace(p.Nombre))
            .GroupBy(p => p.Nombre.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var productosNuevos = new List<Producto>();
        var conflictos = new List<ProductoImportacionPreview>();
        var conflictosPorNombre = new List<ProductoNombreConflicto>();

        foreach (var producto in productosImportados)
        {
            var sku = (producto.Sku ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(sku))
            {
                continue;
            }

            if (diccionarioExistentes.TryGetValue(sku, out var existente))
            {
                conflictos.Add(new ProductoImportacionPreview
                {
                    Sku = sku,
                    NombreActual = existente.Nombre,
                    NombreNuevo = producto.Nombre,
                    CategoriaActual = existente.Categoria,
                    CategoriaNueva = producto.Categoria,
                    ProveedorActual = existente.Proveedor,
                    ProveedorNuevo = producto.Proveedor,
                    StockActual = existente.StockActual,
                    StockNuevo = producto.StockActual,
                    PrecioActual = existente.Precio,
                    PrecioNuevo = producto.Precio,
                    Tipo = "duplicado"
                });
                continue;
            }

            var nombreImportado = (producto.Nombre ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(nombreImportado) && nombresExistentes.TryGetValue(nombreImportado, out var productosMismoNombre))
            {
                conflictosPorNombre.Add(new ProductoNombreConflicto
                {
                    Sku = sku,
                    NombreImportado = nombreImportado,
                    NombreActual = productosMismoNombre.First().Nombre,
                    ProductosCoincidentes = productosMismoNombre.Select(p => new ProductoCoincidente
                    {
                        Id = p.Id,
                        Nombre = p.Nombre,
                        Sku = p.Sku,
                        Categoria = p.Categoria,
                        Precio = p.Precio,
                        StockActual = p.StockActual
                    }).ToList(),
                    NombreSugerido = nombreImportado
                });
                continue;
            }

            productosNuevos.Add(producto);
        }

        return new ImportacionPendiente
        {
            Productos = productosNuevos,
            Conflictos = conflictos,
            ConflictosPorNombre = conflictosPorNombre
        };
    }

    public static Producto? BuscarProductoExistente(List<Producto> productosExistentes, string sku)
    {
        return productosExistentes.FirstOrDefault(p =>
            string.Equals((p.Sku ?? string.Empty).Trim(), (sku ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));
    }
}

public class ProductoCoincidente
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public int StockActual { get; set; }
}

public class ProductoNombreConflicto
{
    public string Sku { get; set; } = string.Empty;
    public string NombreImportado { get; set; } = string.Empty;
    public string NombreActual { get; set; } = string.Empty;
    public string NombreSugerido { get; set; } = string.Empty;
    public List<ProductoCoincidente> ProductosCoincidentes { get; set; } = new();
}

public class ProductoImportacionPreview
{
    public string Sku { get; set; } = string.Empty;
    public string NombreActual { get; set; } = string.Empty;
    public string NombreNuevo { get; set; } = string.Empty;
    public string CategoriaActual { get; set; } = string.Empty;
    public string CategoriaNueva { get; set; } = string.Empty;
    public string ProveedorActual { get; set; } = string.Empty;
    public string ProveedorNuevo { get; set; } = string.Empty;
    public int StockActual { get; set; }
    public int StockNuevo { get; set; }
    public decimal PrecioActual { get; set; }
    public decimal PrecioNuevo { get; set; }
    public string Tipo { get; set; } = "duplicado";
}

public class ImportacionPendiente
{
    public string NombreArchivo { get; set; } = string.Empty;
    public List<Producto> Productos { get; set; } = new();
    public List<ProductoImportacionPreview> Conflictos { get; set; } = new();
    public List<ProductoNombreConflicto> ConflictosPorNombre { get; set; } = new();
}
