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

public class InventoryRepository
{
    private readonly string _connectionString;

    public InventoryRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta la cadena de conexión DefaultConnection.");
    }

    private SqlConnection CrearConexion()
    {
        return new SqlConnection(_connectionString);
    }

    public List<string> ObtenerCategorias(int? empresaId = null)
    {
        using var connection = CrearConexion();
        var sql = @"SELECT DISTINCT categoria FROM Productos WHERE (@EmpresaId IS NULL OR empresaID = @EmpresaId) ORDER BY categoria";
        return connection.Query<string>(sql, new { EmpresaId = empresaId }).ToList();
    }

    public List<Producto> ObtenerProductos(string? sku = null, string? categoria = null, int? empresaId = null)
    {
        var sql = @"SELECT * FROM Productos
                    WHERE (@EmpresaId IS NULL OR empresaID = @EmpresaId)
                      AND (@Sku IS NULL OR sku LIKE '%' + @Sku + '%')
                      AND (@Categoria IS NULL OR categoria = @Categoria)
                    ORDER BY nombre";

        using var connection = CrearConexion();
        return connection.Query<Producto>(sql, new
        {
            EmpresaId = empresaId,
            Sku = string.IsNullOrWhiteSpace(sku) ? null : sku,
            Categoria = string.IsNullOrWhiteSpace(categoria) ? null : categoria
        }).ToList();
    }

    public Producto? BuscarPorId(int id)
    {
        using var connection = CrearConexion();
        return connection.QuerySingleOrDefault<Producto>("SELECT * FROM Productos WHERE id = @Id", new { Id = id });
    }

    public Producto AgregarProducto(Producto producto)
    {
        if (string.IsNullOrWhiteSpace(producto.Nombre) || string.IsNullOrWhiteSpace(producto.Sku))
        {
            throw new ArgumentException("El nombre y el SKU son obligatorios.");
        }

        producto.Nombre = producto.Nombre.Trim();
        producto.Sku = producto.Sku.Trim();
        producto.Categoria = producto.Categoria.Trim();
        producto.Proveedor = producto.Proveedor.Trim();

        if (producto.Precio <= 0)
        {
            throw new ArgumentException($"El producto '{producto.Nombre}' tiene un precio inválido.");
        }

        using var connection = CrearConexion();

        var existe = connection.QuerySingleOrDefault<int?>(
            "SELECT id FROM Productos WHERE empresaID = @EmpresaID AND LOWER(LTRIM(RTRIM(sku))) = @Sku",
            new { producto.EmpresaID, Sku = producto.Sku.ToLowerInvariant() });

        if (existe != null)
        {
            throw new InvalidOperationException($"El SKU '{producto.Sku}' ya existe en la base de datos para esta empresa.");
        }

        var sql = @"INSERT INTO Productos (empresaID, nombre, sku, categoria, descripcion, fechaCarga, precio, stockActual, stockMinimo, proveedor, costoCompra, precioVenta, cantidadVentas)
                    VALUES (@EmpresaID, @Nombre, @Sku, @Categoria, @Descripcion, @FechaCarga, @Precio, @StockActual, @StockMinimo, @Proveedor, @CostoCompra, @PrecioVenta, @CantidadVentas);
                    SELECT CAST(SCOPE_IDENTITY() as int);";

        producto.Id = connection.QuerySingle<int>(sql, producto);
        return producto;
    }

    public bool ActualizarProducto(int id, Producto producto)
    {
        var sql = @"UPDATE Productos
                    SET nombre = @Nombre,
                        sku = @Sku,
                        categoria = @Categoria,
                        descripcion = @Descripcion,
                        precio = @Precio,
                        stockActual = @StockActual,
                        stockMinimo = @StockMinimo,
                        proveedor = @Proveedor,
                        costoCompra = @CostoCompra,
                        precioVenta = @PrecioVenta,
                        cantidadVentas = @CantidadVentas,
                        empresaID = @EmpresaID
                    WHERE id = @Id";

        using var connection = CrearConexion();
        var filasAfectadas = connection.Execute(sql, new
        {
            producto.Nombre,
            producto.Sku,
            producto.Categoria,
            producto.Descripcion,
            producto.Precio,
            producto.StockActual,
            producto.StockMinimo,
            producto.Proveedor,
            producto.CostoCompra,
            producto.PrecioVenta,
            producto.CantidadVentas,
            producto.EmpresaID,
            Id = id
        });

        return filasAfectadas > 0;
    }

    public bool EliminarProducto(int id)
    {
        using var connection = CrearConexion();
        return connection.Execute("DELETE FROM Productos WHERE id = @Id", new { Id = id }) > 0;
    }

    public bool AgregarStock(int id, int cantidad, decimal precioUnitario)
    {
        var sql = @"UPDATE Productos
                    SET stockActual = stockActual + @Cantidad,
                        costoCompra = @PrecioUnitario
                    WHERE id = @Id";

        using var connection = CrearConexion();
        return connection.Execute(sql, new { Id = id, Cantidad = cantidad, PrecioUnitario = precioUnitario }) > 0;
    }

    public DashboardSummary ObtenerDashboard(string range, int? empresaId = null)
    {
        var ventas = ObtenerVentasPorRango(range, empresaId);

        var resumen = new DashboardSummary
        {
            TotalProductos = ObtenerTotalProductos(empresaId),
            ProductosPocoStock = ObtenerProductosConStockBajo(empresaId),
            ProductosSinStock = ObtenerProductosSinStock(empresaId),
            GananciasPorMesNetas = ventas.Sum(v => v.Precio - v.Costo),
            VentasTotales = ventas.Count,
            VentaPromedioPorMes = ventas.Any() ? ventas.Average(v => v.Precio) : 0m,
            UltimosProductosVendidos = ventas.OrderByDescending(v => v.Fecha).Take(5).ToList(),
            BasesDeDatos = new List<string> { "Productos", "Ventas", "Proveedores", "Dashboard" }
        };

        var mesMayor = ventas
            .GroupBy(v => new { v.Fecha.Year, v.Fecha.Month })
            .Select(g => new { Mes = new DateTime(g.Key.Year, g.Key.Month, 1), Total = g.Sum(x => x.Precio) })
            .OrderByDescending(x => x.Total)
            .FirstOrDefault();

        resumen.MesMayorVentas = mesMayor == null ? "Sin ventas" : mesMayor.Mes.ToString("MMMM yyyy");

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

    private int ObtenerTotalProductos(int? empresaId = null)
    {
        using var connection = CrearConexion();
        var sql = "SELECT COUNT(*) FROM Productos WHERE (@EmpresaId IS NULL OR empresaID = @EmpresaId)";
        return connection.QuerySingle<int>(sql, new { EmpresaId = empresaId });
    }

    private int ObtenerProductosConStockBajo(int? empresaId = null)
    {
        using var connection = CrearConexion();
        var sql = "SELECT COUNT(*) FROM Productos WHERE (@EmpresaId IS NULL OR empresaID = @EmpresaId) AND stockActual <= stockMinimo";
        return connection.QuerySingle<int>(sql, new { EmpresaId = empresaId });
    }

    private int ObtenerProductosSinStock(int? empresaId = null)
    {
        using var connection = CrearConexion();
        var sql = "SELECT COUNT(*) FROM Productos WHERE (@EmpresaId IS NULL OR empresaID = @EmpresaId) AND stockActual <= 0";
        return connection.QuerySingle<int>(sql, new { EmpresaId = empresaId });
    }

    private List<VentaRegistro> ObtenerVentasPorRango(string range, int? empresaId = null)
    {
        var fechaInicio = range.ToLowerInvariant() switch
        {
            "dia" => DateTime.Now.AddDays(-1),
            "semana" => DateTime.Now.AddDays(-7),
            "mes" => DateTime.Now.AddMonths(-1),
            "anio" => DateTime.Now.AddYears(-1),
            _ => DateTime.Now.AddMonths(-1)
        };

        using var connection = CrearConexion();
        return connection.Query<VentaRegistro>(
            "SELECT * FROM Ventas WHERE productoId IN (SELECT id FROM Productos WHERE (@EmpresaId IS NULL OR empresaID = @EmpresaId)) AND fecha >= @FechaInicio ORDER BY fecha DESC",
            new { EmpresaId = empresaId, FechaInicio = fechaInicio }).ToList();
    }

    public bool TieneProductos(int empresaId)
    {
        using var connection = CrearConexion();
        return connection.QuerySingle<int>("SELECT COUNT(*) FROM Productos WHERE empresaID = @EmpresaId", new { EmpresaId = empresaId }) > 0;
    }
}
