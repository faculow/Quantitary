using Microsoft.AspNetCore.Http;

namespace Quantitary.Services;

public class Producto
{
    public int Id { get; set; }
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
}

public class VentaRegistro
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public string NombreProducto { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string MetodoPago { get; set; } = string.Empty;
    public int Cantidad { get; set; } = 1;
    public decimal Precio { get; set; }
    public decimal Costo { get; set; }
}

public class ProductoRanking
{
    public string Nombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal Total { get; set; }
}

public class DashboardSummary
{
    public int TotalProductos { get; set; }
    public int ProductosPocoStock { get; set; }
    public int ProductosSinStock { get; set; }
    public decimal GananciasPorMesNetas { get; set; }
    public int VentasTotales { get; set; }
    public string MesMayorVentas { get; set; } = string.Empty;
    public decimal VentaPromedioPorMes { get; set; }
    public List<ProductoRanking> PodioProductosMasVendidos { get; set; } = new();
    public List<ProductoRanking> PodioProductosMasGanancia { get; set; } = new();
    public List<VentaRegistro> UltimosProductosVendidos { get; set; } = new();
    public List<string> BasesDeDatos { get; set; } = new();
}

public class InventoryService
{
    private readonly List<Producto> _productos = new()
    {
        new Producto { Id = 1, Nombre = "Laptop Pro 14", Sku = "LAP-001", Categoria = "Tecnología", Descripcion = "Notebook premium para trabajo", FechaCarga = new DateTime(2026, 9, 5), Precio = 1299.99m, StockActual = 8, StockMinimo = 4, Proveedor = "Tech Supply", CostoCompra = 950m, PrecioVenta = 1299.99m },
        new Producto { Id = 2, Nombre = "Mouse Gamer X", Sku = "MOU-002", Categoria = "Tecnología", Descripcion = "Mouse inalámbrico con alta sensibilidad", FechaCarga = new DateTime(2026, 9, 10), Precio = 79.90m, StockActual = 2, StockMinimo = 5, Proveedor = "Constelación", CostoCompra = 42m, PrecioVenta = 79.90m },
        new Producto { Id = 3, Nombre = "Cafetera Deluxe", Sku = "CAF-003", Categoria = "Hogar", Descripcion = "Preparación automática y silenciosa", FechaCarga = new DateTime(2026, 9, 12), Precio = 249.00m, StockActual = 0, StockMinimo = 2, Proveedor = "Domicilios Uno", CostoCompra = 170m, PrecioVenta = 249.00m },
        new Producto { Id = 4, Nombre = "Silla Ergonómica", Sku = "SIL-004", Categoria = "Oficina", Descripcion = "Silla con soporte lumbar y ruedas", FechaCarga = new DateTime(2026, 9, 13), Precio = 189.50m, StockActual = 15, StockMinimo = 6, Proveedor = "Muebles Plus", CostoCompra = 110m, PrecioVenta = 189.50m },
        new Producto { Id = 5, Nombre = "Batería 5000mAh", Sku = "BAT-005", Categoria = "Accesorios", Descripcion = "Batería recargable para equipos", FechaCarga = new DateTime(2026, 9, 20), Precio = 59.99m, StockActual = 11, StockMinimo = 5, Proveedor = "Energia Latam", CostoCompra = 32m, PrecioVenta = 59.99m },
        new Producto { Id = 6, Nombre = "Impresora Multifuncional", Sku = "IMP-006", Categoria = "Tecnología", Descripcion = "Impresión, escaneo y fotocopia", FechaCarga = new DateTime(2026, 9, 25), Precio = 399.00m, StockActual = 3, StockMinimo = 3, Proveedor = "Print Center", CostoCompra = 260m, PrecioVenta = 399.00m }
    };

    private readonly List<VentaRegistro> _ventas = new()
    {
        new VentaRegistro { Id = 1, ProductoId = 1, NombreProducto = "Laptop Pro 14", Fecha = DateTime.Now.AddDays(-1), MetodoPago = "Tarjeta", Cantidad = 2, Precio = 2599.98m, Costo = 1900m },
        new VentaRegistro { Id = 2, ProductoId = 4, NombreProducto = "Silla Ergonómica", Fecha = DateTime.Now.AddDays(-2), MetodoPago = "Transferencia", Cantidad = 3, Precio = 568.50m, Costo = 330m },
        new VentaRegistro { Id = 3, ProductoId = 5, NombreProducto = "Batería 5000mAh", Fecha = DateTime.Now.AddDays(-4), MetodoPago = "Efectivo", Cantidad = 6, Precio = 359.94m, Costo = 192m },
        new VentaRegistro { Id = 4, ProductoId = 2, NombreProducto = "Mouse Gamer X", Fecha = DateTime.Now.AddDays(-6), MetodoPago = "Tarjeta", Cantidad = 4, Precio = 319.60m, Costo = 168m },
        new VentaRegistro { Id = 5, ProductoId = 6, NombreProducto = "Impresora Multifuncional", Fecha = DateTime.Now.AddDays(-10), MetodoPago = "Transferencia", Cantidad = 1, Precio = 399.00m, Costo = 260m },
        new VentaRegistro { Id = 6, ProductoId = 3, NombreProducto = "Cafetera Deluxe", Fecha = DateTime.Now.AddDays(-15), MetodoPago = "Tarjeta", Cantidad = 2, Precio = 498.00m, Costo = 340m },
        new VentaRegistro { Id = 7, ProductoId = 1, NombreProducto = "Laptop Pro 14", Fecha = DateTime.Now.AddDays(-20), MetodoPago = "Efectivo", Cantidad = 1, Precio = 1299.99m, Costo = 950m },
        new VentaRegistro { Id = 8, ProductoId = 4, NombreProducto = "Silla Ergonómica", Fecha = DateTime.Now.AddDays(-25), MetodoPago = "Transferencia", Cantidad = 2, Precio = 379.00m, Costo = 220m }
    };

    public DashboardSummary ObtenerDashboard(string range)
    {
        var ventas = FiltrarVentasPorRango(range);
        var resumen = new DashboardSummary
        {
            TotalProductos = _productos.Count,
            ProductosPocoStock = _productos.Count(p => p.StockActual <= p.StockMinimo),
            ProductosSinStock = _productos.Count(p => p.StockActual <= 0),
            GananciasPorMesNetas = ventas.Sum(v => v.Precio - v.Costo),
            VentasTotales = ventas.Count,
            VentaPromedioPorMes = ventas.Any() ? ventas.Average(v => v.Precio) : 0m,
            UltimosProductosVendidos = ventas.OrderByDescending(v => v.Fecha).Take(5).ToList(),
            BasesDeDatos = new List<string> { "Productos", "Ventas", "Proveedores", "Dashboard" }
        };

        var agrupadoMes = ventas
            .GroupBy(v => new { v.Fecha.Year, v.Fecha.Month })
            .Select(g => new { Mes = new DateTime(g.Key.Year, g.Key.Month, 1), Total = g.Sum(x => x.Precio) })
            .OrderByDescending(x => x.Total)
            .FirstOrDefault();

        resumen.MesMayorVentas = agrupadoMes == null ? "Sin ventas" : agrupadoMes.Mes.ToString("MMMM yyyy");

        resumen.PodioProductosMasVendidos = ventas
            .GroupBy(v => v.NombreProducto)
            .Select(g => new ProductoRanking
            {
                Nombre = g.Key,
                Cantidad = g.Sum(v => v.Cantidad),
                Total = g.Sum(v => v.Precio)
            })
            .OrderByDescending(x => x.Cantidad)
            .Take(3)
            .ToList();

        resumen.PodioProductosMasGanancia = ventas
            .GroupBy(v => v.NombreProducto)
            .Select(g => new ProductoRanking
            {
                Nombre = g.Key,
                Cantidad = g.Sum(v => v.Cantidad),
                Total = g.Sum(v => v.Precio - v.Costo)
            })
            .OrderByDescending(x => x.Total)
            .Take(3)
            .ToList();

        return resumen;
    }

    public List<Producto> ObtenerProductos(string? sku = null, string? categoria = null)
    {
        IEnumerable<Producto> query = _productos;

        if (!string.IsNullOrWhiteSpace(sku))
        {
            query = query.Where(p => p.Sku.Contains(sku, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(categoria))
        {
            query = query.Where(p => p.Categoria.Contains(categoria, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderBy(p => p.Nombre).ToList();
    }

    public Producto? BuscarPorSku(string sku)
    {
        return _productos.FirstOrDefault(p => p.Sku.Equals(sku.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public Producto AgregarProducto(Producto producto)
    {
        if (string.IsNullOrWhiteSpace(producto.Nombre) || string.IsNullOrWhiteSpace(producto.Sku))
        {
            throw new ArgumentException("El nombre y el SKU son obligatorios.");
        }

        producto.Id = _productos.Any() ? _productos.Max(p => p.Id) + 1 : 1;
        producto.FechaCarga = DateTime.Now;
        _productos.Add(producto);
        return producto;
    }

    public Producto? ActualizarProducto(int id, Producto producto)
    {
        var actual = _productos.FirstOrDefault(p => p.Id == id);
        if (actual == null) return null;

        actual.Nombre = producto.Nombre;
        actual.Sku = producto.Sku;
        actual.Categoria = producto.Categoria;
        actual.Descripcion = producto.Descripcion;
        actual.Precio = producto.Precio;
        actual.StockActual = producto.StockActual;
        actual.StockMinimo = producto.StockMinimo;
        actual.Proveedor = producto.Proveedor;
        actual.CostoCompra = producto.CostoCompra;
        actual.PrecioVenta = producto.PrecioVenta ?? producto.Precio;

        return actual;
    }

    public bool EliminarProducto(int id)
    {
        var producto = _productos.FirstOrDefault(p => p.Id == id);
        if (producto == null) return false;

        _productos.Remove(producto);
        return true;
    }

    public Producto? AgregarStock(int id, int cantidad, decimal precioUnitario)
    {
        var producto = _productos.FirstOrDefault(p => p.Id == id);
        if (producto == null || cantidad <= 0) return null;

        producto.StockActual += cantidad;
        producto.CostoCompra = precioUnitario;
        return producto;
    }

    public List<Producto> CargarCatalogo(IFormFile archivo)
    {
        using var reader = new StreamReader(archivo.OpenReadStream());
        var contenido = reader.ReadToEnd();
        var lineas = contenido.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        var productosAgregados = new List<Producto>();

        foreach (var linea in lineas.Skip(1))
        {
            var columnas = linea.Split(',');
            if (columnas.Length < 6) continue;

            var producto = new Producto
            {
                Nombre = columnas[0].Trim(),
                Sku = columnas[1].Trim(),
                Categoria = columnas[2].Trim(),
                Descripcion = columnas[3].Trim(),
                FechaCarga = DateTime.TryParse(columnas[4], out var fecha) ? fecha : DateTime.Now,
                Precio = decimal.TryParse(columnas[5], out var precio) ? precio : 0m,
                StockActual = 0,
                StockMinimo = 0,
                Proveedor = "Catalogo importado"
            };

            if (!string.IsNullOrWhiteSpace(producto.Nombre) && !string.IsNullOrWhiteSpace(producto.Sku))
            {
                AgregarProducto(producto);
                productosAgregados.Add(producto);
            }
        }

        return productosAgregados;
    }

    public List<string> ObtenerCategorias()
    {
        return _productos.Select(p => p.Categoria).Distinct().OrderBy(x => x).ToList();
    }

    private List<VentaRegistro> FiltrarVentasPorRango(string range)
    {
        var hoy = DateTime.Now;
        var fechaInicio = range.ToLowerInvariant() switch
        {
            "dia" => hoy.AddDays(-1),
            "semana" => hoy.AddDays(-7),
            "mes" => hoy.AddMonths(-1),
            "anio" => hoy.AddYears(-1),
            _ => hoy.AddMonths(-1)
        };

        return _ventas.Where(v => v.Fecha >= fechaInicio).ToList();
    }
}
