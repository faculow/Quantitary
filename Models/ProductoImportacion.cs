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
        "SKU",
        "Nombre",
        "Categoria",
        "Precio",
        "Proveedor",
        "CantidadVentas"
    };

    public static readonly Dictionary<string, string[]> AliasColumnas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SKU"] = ["SKU", "Sku", "sku", "Codigo", "Cod", "Código"],
        ["Nombre"] = ["Nombre", "nombre", "Producto", "DescripcionCorta"],
        ["Categoria"] = ["Categoria", "categoria", "Categoría", "Rubro"],
        ["Precio"] = ["Precio", "precio", "PrecioUnitario", "Valor"],
        ["Proveedor"] = ["Proveedor", "proveedor", "Fabricante", "Marca"],
        ["CantidadVentas"] = ["CantidadVentas", "cantidadVentas", "Ventas", "Cantidad de ventas", "VentasTotales"]
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
                StockActual = 0,
                StockMinimo = 0,
                CostoCompra = 0m,
                PrecioVenta = 0m,
                CantidadVentas = ObtenerEntero(fila, columnas, "CantidadVentas")
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
            columnas[columna.ColumnName.Trim()] = columna.Ordinal;
        }

        var faltantes = ProductoImportacionExcel.ColumnasEsperadas
            .Where(columnaEsperada => !columnas.Keys.Any(columnaReal => ProductoImportacionExcel.AliasColumnas[columnaEsperada]
                .Any(alias => string.Equals(columnaReal.Trim(), alias, StringComparison.OrdinalIgnoreCase))))
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
            if (columnas.TryGetValue(alias, out var indice))
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
}
