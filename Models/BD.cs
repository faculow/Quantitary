using System.Data;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using System.Globalization;
using Quantitary.Services;

namespace Quantitary.Models;

public static class BD
{
    private sealed class UsuarioSesion
    {
        public int Id { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
        public int EmpresaId { get; set; }
        public string NombreEmpresa { get; set; } = string.Empty;
    }

    public static string ObtenerCadenaConexion(IConfiguration configuration)
    {
        return configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta la cadena de conexión DefaultConnection.");
    }

    public static IDbConnection CrearConexion(IConfiguration configuration)
    {
        return new SqlConnection(ObtenerCadenaConexion(configuration));
    }

    private static bool ExisteColumna(IDbConnection connection, string nombreTabla, string nombreColumna)
    {
        var sql = @"SELECT CASE WHEN EXISTS (
                        SELECT 1
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_SCHEMA = 'dbo'
                          AND TABLE_NAME = @NombreTabla
                          AND COLUMN_NAME = @NombreColumna) THEN 1 ELSE 0 END;";

        return connection.QuerySingle<int>(sql, new { NombreTabla = nombreTabla, NombreColumna = nombreColumna }) == 1;
    }

    private static bool ExisteTabla(IDbConnection connection, string nombreTabla)
    {
        var sql = "SELECT CASE WHEN OBJECT_ID(@NombreTabla, 'U') IS NOT NULL THEN 1 ELSE 0 END;";
        return connection.QuerySingle<int>(sql, new { NombreTabla = nombreTabla }) == 1;
    }

    private static bool UsaEsquemaNormalizado(IConfiguration configuration)
    {
        using var connection = CrearConexion(configuration);
        return ExisteColumna(connection, "Productos", "nombre")
            && ExisteColumna(connection, "Productos", "precioVenta")
            && ExisteColumna(connection, "Productos", "categoriaID")
            && ExisteColumna(connection, "Productos", "activo")
            && ExisteColumna(connection, "Stock", "cantidadMinimoAlerta")
            && ExisteColumna(connection, "Proveedores", "nombre");
    }

    public static string NormalizarMail(string? mail)
    {
        return (mail ?? string.Empty).Trim();
    }

    public static List<string> ObtenerCategorias(IConfiguration configuration, int? empresaId = null)
    {
        using var connection = CrearConexion(configuration);

        if (!UsaEsquemaNormalizado(configuration))
        {
            return connection.Query<string>(@"
                SELECT DISTINCT CAST(categoria AS varchar(100)) AS categoria
                FROM Productos
                WHERE (@EmpresaId IS NULL OR empresaID = @EmpresaId)
                  AND LTRIM(RTRIM(COALESCE(categoria, ''))) <> ''
                ORDER BY categoria;",
                new { EmpresaId = empresaId == 0 ? null : empresaId }).ToList();
        }

        return connection.Query<string>(@"
            SELECT DISTINCT c.nombre AS categoria
            FROM Productos p
            INNER JOIN Categoria c ON c.id = p.categoriaID
            WHERE (@EmpresaId IS NULL OR p.empresaID = @EmpresaId)
              AND LTRIM(RTRIM(c.nombre)) <> ''
            ORDER BY c.nombre;",
            new { EmpresaId = empresaId == 0 ? null : empresaId }).ToList();
    }

    public static List<Producto> ObtenerProductos(IConfiguration configuration, string? sku = null, string? categoria = null, bool? pocoStock = null, decimal? precioMinimo = null, string? orden = null, int? empresaId = null)
    {
        using var connection = CrearConexion(configuration);
        var parametros = new DynamicParameters();
        parametros.Add("@EmpresaId", empresaId == 0 ? null : empresaId);
        parametros.Add("@Sku", string.IsNullOrWhiteSpace(sku) ? null : sku.Trim());
        parametros.Add("@Categoria", string.IsNullOrWhiteSpace(categoria) ? null : categoria.Trim());
        parametros.Add("@PocoStock", pocoStock == true ? 1 : 0);
        parametros.Add("@PrecioMinimo", precioMinimo.HasValue && precioMinimo.Value > 0 ? precioMinimo.Value : null);
        parametros.Add("@Orden", string.IsNullOrWhiteSpace(orden) ? "nombre_asc" : orden.Trim());

        if (!UsaEsquemaNormalizado(configuration))
        {
            return connection.Query<Producto>(@"
                SELECT p.id AS Id,
                       p.empresaID AS EmpresaID,
                       0 AS CategoriaID,
                       NULL AS ProveedorID,
                       p.nombre AS Nombre,
                       p.sku AS Sku,
                       COALESCE(p.categoria, 'Sin categoría') AS Categoria,
                       p.descripcion AS Descripcion,
                       p.fechaCarga AS FechaCarga,
                       CAST(COALESCE(p.precioVenta, p.precio, 0) AS decimal(12,2)) AS PrecioVenta,
                       CAST(COALESCE(p.costoCompra, 0) AS decimal(12,2)) AS CostoCompra,
                       CAST(COALESCE(p.stockActual, 0) AS int) AS StockActual,
                       CAST(COALESCE(p.stockMinimo, 0) AS int) AS StockMinimo,
                       CAST(1 AS bit) AS Activo,
                       COALESCE(p.proveedor, '') AS Proveedor
                FROM Productos p
                WHERE (@EmpresaId IS NULL OR p.empresaID = @EmpresaId)
                  AND (@Sku IS NULL OR LOWER(LTRIM(RTRIM(p.sku))) LIKE '%' + LOWER(LTRIM(RTRIM(@Sku))) + '%')
                  AND (@Categoria IS NULL OR LOWER(LTRIM(RTRIM(p.categoria))) = LOWER(LTRIM(RTRIM(@Categoria))))
                  AND (@PocoStock = 0 OR COALESCE(p.stockActual, 0) <= COALESCE(p.stockMinimo, 0) OR COALESCE(p.stockActual, 0) = 0)
                  AND (@PrecioMinimo IS NULL OR CAST(COALESCE(p.precioVenta, p.precio, 0) AS decimal(12,2)) >= @PrecioMinimo)
                ORDER BY CASE WHEN @Orden = 'nombre_asc' THEN p.nombre END ASC,
                         CASE WHEN @Orden = 'nombre_desc' THEN p.nombre END DESC,
                         CASE WHEN @Orden = 'precio_asc' THEN COALESCE(p.precioVenta, p.precio, 0) END ASC,
                         CASE WHEN @Orden = 'precio_desc' THEN COALESCE(p.precioVenta, p.precio, 0) END DESC,
                         CASE WHEN @Orden = 'stock_asc' THEN COALESCE(p.stockActual, 0) END ASC,
                         CASE WHEN @Orden = 'stock_desc' THEN COALESCE(p.stockActual, 0) END DESC,
                         CASE WHEN @Orden = 'sku_asc' THEN p.sku END ASC,
                         CASE WHEN @Orden = 'sku_desc' THEN p.sku END DESC;",
                parametros).ToList();
        }

        return connection.Query<Producto>(@"
            SELECT p.id AS Id,
                   p.empresaID AS EmpresaID,
                   p.categoriaID AS CategoriaID,
                   p.proveedorID AS ProveedorID,
                   p.nombre AS Nombre,
                   p.sku AS Sku,
                   c.nombre AS Categoria,
                   p.descripcion AS Descripcion,
                   p.fechaCarga AS FechaCarga,
                   p.precioVenta AS PrecioVenta,
                   p.costoCompra AS CostoCompra,
                   ISNULL(s.cantidad, 0) AS StockActual,
                   ISNULL(s.cantidadMinimoAlerta, 0) AS StockMinimo,
                   p.activo AS Activo,
                   pe.nombre AS Proveedor
            FROM Productos p
            LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
            LEFT JOIN Categoria c ON c.id = p.categoriaID
            LEFT JOIN Proveedores pe ON pe.id = p.proveedorID
            WHERE (@EmpresaId IS NULL OR p.empresaID = @EmpresaId)
              AND (@Sku IS NULL OR LOWER(LTRIM(RTRIM(p.sku))) LIKE '%' + LOWER(LTRIM(RTRIM(@Sku))) + '%')
              AND (@Categoria IS NULL OR LOWER(LTRIM(RTRIM(c.nombre))) = LOWER(LTRIM(RTRIM(@Categoria))))
              AND (@PocoStock = 0 OR ISNULL(s.cantidad, 0) <= ISNULL(s.cantidadMinimoAlerta, 0) OR ISNULL(s.cantidad, 0) = 0)
              AND (@PrecioMinimo IS NULL OR p.precioVenta >= @PrecioMinimo)
            ORDER BY CASE WHEN @Orden = 'nombre_asc' THEN p.nombre END ASC,
                     CASE WHEN @Orden = 'nombre_desc' THEN p.nombre END DESC,
                     CASE WHEN @Orden = 'precio_asc' THEN p.precioVenta END ASC,
                     CASE WHEN @Orden = 'precio_desc' THEN p.precioVenta END DESC,
                     CASE WHEN @Orden = 'stock_asc' THEN ISNULL(s.cantidad, 0) END ASC,
                     CASE WHEN @Orden = 'stock_desc' THEN ISNULL(s.cantidad, 0) END DESC,
                     CASE WHEN @Orden = 'sku_asc' THEN p.sku END ASC,
                     CASE WHEN @Orden = 'sku_desc' THEN p.sku END DESC;",
            parametros).ToList();
    }

    public static Producto? BuscarPorId(IConfiguration configuration, int id)
    {
        using var connection = CrearConexion(configuration);

        if (!UsaEsquemaNormalizado(configuration))
        {
            return connection.QuerySingleOrDefault<Producto>(@"
                SELECT p.id AS Id,
                       p.empresaID AS EmpresaID,
                       0 AS CategoriaID,
                       NULL AS ProveedorID,
                       p.nombre AS Nombre,
                       p.sku AS Sku,
                       COALESCE(p.categoria, 'Sin categoría') AS Categoria,
                       p.descripcion AS Descripcion,
                       p.fechaCarga AS FechaCarga,
                       CAST(COALESCE(p.precioVenta, p.precio, 0) AS decimal(12,2)) AS PrecioVenta,
                       CAST(COALESCE(p.costoCompra, 0) AS decimal(12,2)) AS CostoCompra,
                       CAST(COALESCE(p.stockActual, 0) AS int) AS StockActual,
                       CAST(COALESCE(p.stockMinimo, 0) AS int) AS StockMinimo,
                       CAST(1 AS bit) AS Activo,
                       COALESCE(p.proveedor, '') AS Proveedor
                FROM Productos p
                WHERE p.id = @Id;", new { Id = id });
        }

        return connection.QuerySingleOrDefault<Producto>(@"
            SELECT p.id AS Id,
                   p.empresaID AS EmpresaID,
                   p.categoriaID AS CategoriaID,
                   p.proveedorID AS ProveedorID,
                   p.nombre AS Nombre,
                   p.sku AS Sku,
                   c.nombre AS Categoria,
                   p.descripcion AS Descripcion,
                   p.fechaCarga AS FechaCarga,
                   p.precioVenta AS PrecioVenta,
                   p.costoCompra AS CostoCompra,
                   ISNULL(s.cantidad, 0) AS StockActual,
                   ISNULL(s.cantidadMinimoAlerta, 0) AS StockMinimo,
                   p.activo AS Activo,
                   pe.nombre AS Proveedor
            FROM Productos p
            LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
            LEFT JOIN Categoria c ON c.id = p.categoriaID
            LEFT JOIN Proveedores pe ON pe.id = p.proveedorID
            WHERE p.id = @Id;", new { Id = id });
    }

    private static int ObtenerOCrearCategoriaId(IDbConnection connection, int empresaId, string? categoriaNombre, IDbTransaction? transaction = null)
    {
        var nombre = string.IsNullOrWhiteSpace(categoriaNombre) ? "Sin categoría" : categoriaNombre.Trim();

        var categoriaId = connection.QuerySingleOrDefault<int?>(@"
            SELECT TOP 1 id
            FROM Categoria
            WHERE empresaID = @EmpresaId
              AND LOWER(LTRIM(RTRIM(nombre))) = LOWER(LTRIM(RTRIM(@Nombre)))",
            new { EmpresaId = empresaId, Nombre = nombre }, transaction) ?? 0;

        if (categoriaId > 0) return categoriaId;

        return connection.QuerySingle<int>(@"
            INSERT INTO Categoria (empresaID, nombre)
            VALUES (@EmpresaId, @Nombre);
            SELECT CAST(SCOPE_IDENTITY() AS int);",
            new { EmpresaId = empresaId, Nombre = nombre }, transaction);
    }

    public static Producto AgregarProducto(IConfiguration configuration, Producto producto)
    {
        if (string.IsNullOrWhiteSpace(producto.Nombre) || string.IsNullOrWhiteSpace(producto.Sku))
            throw new ArgumentException("El nombre y el SKU son obligatorios.");

        producto.Nombre = producto.Nombre.Trim();
        producto.Sku = producto.Sku.Trim();
        producto.Proveedor = string.IsNullOrWhiteSpace(producto.Proveedor) ? string.Empty : producto.Proveedor.Trim();
        producto.Categoria = string.IsNullOrWhiteSpace(producto.Categoria) ? "Sin categoría" : producto.Categoria.Trim();

        if (producto.PrecioVenta <= 0)
            throw new ArgumentException($"El producto '{producto.Nombre}' tiene un precio inválido.");

        using var connection = CrearConexion(configuration);
        producto.CategoriaID = ObtenerOCrearCategoriaId(connection, producto.EmpresaID, producto.Categoria);
        producto.ProveedorID = ObtenerOCrearProveedorId(connection, producto.EmpresaID, producto.Proveedor);

        var skuDuplicado = connection.ExecuteScalar<int>(@"
            SELECT COUNT(1)
            FROM Productos
            WHERE empresaID = @EmpresaID
              AND LOWER(LTRIM(RTRIM(sku))) = LOWER(LTRIM(RTRIM(@Sku)));",
            new { producto.EmpresaID, producto.Sku });

        if (skuDuplicado > 0)
            throw new ArgumentException($"Ya existe un producto con el SKU '{producto.Sku}'.");

        var sql = @"INSERT INTO Productos (empresaID, categoriaID, proveedorID, nombre, sku, descripcion, fechaCarga, precioVenta, costoCompra, activo)
                    VALUES (@EmpresaID, @CategoriaID, @ProveedorID, @Nombre, @Sku, @Descripcion, @FechaCarga, @PrecioVenta, @CostoCompra, @Activo);
                    SELECT CAST(SCOPE_IDENTITY() as int);";

        producto.Id = connection.QuerySingle<int>(sql, producto);

        connection.Execute(@"INSERT INTO Stock (productoID, cantidad, cantidadMinimoAlerta, activo, actualizadoEn)
                            VALUES (@ProductoId, @Cantidad, @CantidadMinimoAlerta, @Activo, @ActualizadoEn);",
            new
            {
                ProductoId = producto.Id,
                Cantidad = producto.StockActual,
                CantidadMinimoAlerta = producto.StockMinimo,
                Activo = true,
                ActualizadoEn = DateTime.Now
            });

        return producto;
    }

    public static bool ActualizarProducto(IConfiguration configuration, int id, Producto producto)
    {
        if (string.IsNullOrWhiteSpace(producto.Nombre) || string.IsNullOrWhiteSpace(producto.Sku))
            throw new ArgumentException("El nombre y el SKU son obligatorios.");

        producto.Nombre = producto.Nombre.Trim();
        producto.Sku = producto.Sku.Trim();
        producto.Categoria = string.IsNullOrWhiteSpace(producto.Categoria) ? "Sin categoría" : producto.Categoria.Trim();
        producto.Proveedor = string.IsNullOrWhiteSpace(producto.Proveedor) ? string.Empty : producto.Proveedor.Trim();

        if (producto.PrecioVenta <= 0)
            throw new ArgumentException($"El producto '{producto.Nombre}' tiene un precio inválido.");

        using var connection = CrearConexion(configuration);

        var empresaId = connection.ExecuteScalar<int?>(@"SELECT empresaID FROM Productos WHERE id = @Id;", new { Id = id });
        if (!empresaId.HasValue) return false;

        producto.CategoriaID = ObtenerOCrearCategoriaId(connection, empresaId.Value, producto.Categoria);
        producto.ProveedorID = ObtenerOCrearProveedorId(connection, empresaId.Value, producto.Proveedor);

        var skuDuplicado = connection.ExecuteScalar<int>(@"
            SELECT COUNT(1)
            FROM Productos
            WHERE empresaID = @EmpresaID
              AND id <> @Id
              AND LOWER(LTRIM(RTRIM(sku))) = LOWER(LTRIM(RTRIM(@Sku)));",
            new { EmpresaID = empresaId.Value, Id = id, producto.Sku });

        if (skuDuplicado > 0)
            throw new ArgumentException($"No se puede guardar: ya existe otro producto con el SKU '{producto.Sku}'.");

        var filasAfectadas = connection.Execute(@"UPDATE Productos
                    SET nombre = @Nombre,
                        sku = @Sku,
                        categoriaID = @CategoriaID,
                        proveedorID = @ProveedorID,
                        descripcion = @Descripcion,
                        precioVenta = @PrecioVenta,
                        costoCompra = @CostoCompra,
                        activo = @Activo
                    WHERE id = @Id;",
            new
            {
                producto.Nombre,
                producto.Sku,
                producto.CategoriaID,
                producto.ProveedorID,
                producto.Descripcion,
                producto.PrecioVenta,
                producto.CostoCompra,
                producto.Activo,
                Id = id
            });

        connection.Execute(@"UPDATE Stock
                            SET cantidad = @Cantidad,
                                cantidadMinimoAlerta = @CantidadMinimoAlerta,
                                actualizadoEn = @ActualizadoEn
                            WHERE productoID = @ProductoId;",
            new
            {
                Cantidad = producto.StockActual,
                CantidadMinimoAlerta = producto.StockMinimo,
                ActualizadoEn = DateTime.Now,
                ProductoId = id
            });

        return filasAfectadas > 0;
    }

    private static int? ObtenerOCrearProveedorId(IDbConnection connection, int empresaId, string? proveedorNombre, IDbTransaction? transaction = null)
    {
        var nombre = string.IsNullOrWhiteSpace(proveedorNombre) ? null : proveedorNombre.Trim();
        if (string.IsNullOrWhiteSpace(nombre)) return null;

        var proveedorId = connection.QuerySingleOrDefault<int?>(@"
            SELECT TOP 1 id
            FROM Proveedores
            WHERE empresaID = @EmpresaId
              AND LOWER(LTRIM(RTRIM(nombre))) = LOWER(LTRIM(RTRIM(@Nombre)))",
            new { EmpresaId = empresaId, Nombre = nombre }, transaction) ?? 0;

        if (proveedorId > 0) return proveedorId;

        return connection.QuerySingle<int>(@"
            INSERT INTO Proveedores (empresaID, nombre)
            VALUES (@EmpresaId, @Nombre);
            SELECT CAST(SCOPE_IDENTITY() AS int);",
            new { EmpresaId = empresaId, Nombre = nombre }, transaction);
    }

    public static List<string> ObtenerProveedores(IConfiguration configuration, int? empresaId = null)
    {
        using var connection = CrearConexion(configuration);
        var sql = @"SELECT DISTINCT pe.nombre AS proveedor
                    FROM Productos p
                    INNER JOIN Proveedores pe ON pe.id = p.proveedorID
                    WHERE (@EmpresaId IS NULL OR p.empresaID = @EmpresaId)
                      AND LTRIM(RTRIM(pe.nombre)) <> ''
                    ORDER BY pe.nombre;";
        return connection.Query<string>(sql, new { EmpresaId = empresaId == 0 ? null : empresaId }).ToList();
    }

    public static List<string> ObtenerProveedoresPorProducto(IConfiguration configuration, int productoId)
    {
        using var connection = CrearConexion(configuration);
        var sql = @"SELECT DISTINCT pe.nombre AS proveedor
                    FROM Productos p
                    INNER JOIN Proveedores pe ON pe.id = p.proveedorID
                    WHERE p.id = @ProductoId
                      AND LTRIM(RTRIM(pe.nombre)) <> ''
                    ORDER BY pe.nombre;";
        return connection.Query<string>(sql, new { ProductoId = productoId }).ToList();
    }

    public static List<Categoria> ObtenerCategoriasCompleto(IConfiguration configuration, int? empresaId = null)
    {
        using var connection = CrearConexion(configuration);
        var sql = @"SELECT c.id, c.nombre, COUNT(p.id) AS cantidadProductos
                    FROM Categoria c
                    LEFT JOIN Productos p ON p.categoriaID = c.id AND p.activo = 1
                    WHERE (@EmpresaId IS NULL OR c.empresaID = @EmpresaId)
                    GROUP BY c.id, c.nombre
                    ORDER BY c.nombre;";
        return connection.Query<Categoria>(sql, new { EmpresaId = empresaId == 0 ? null : empresaId }).ToList();
    }

    public static List<Proveedor> ObtenerProveedoresCompleto(IConfiguration configuration, int? empresaId = null)
    {
        using var connection = CrearConexion(configuration);
        var sql = @"SELECT pe.id, pe.nombre, COUNT(p.id) AS cantidadProductos
                    FROM Proveedores pe
                    LEFT JOIN Productos p ON p.proveedorID = pe.id AND p.activo = 1
                    WHERE (@EmpresaId IS NULL OR pe.empresaID = @EmpresaId)
                    GROUP BY pe.id, pe.nombre
                    ORDER BY pe.nombre;";
        return connection.Query<Proveedor>(sql, new { EmpresaId = empresaId == 0 ? null : empresaId }).ToList();
    }

    public static List<StockRegistro> ObtenerStock(IConfiguration configuration, int? empresaId = null)
    {
        using var connection = CrearConexion(configuration);
        var sql = @"SELECT p.id AS ProductoId, p.nombre AS NombreProducto, p.sku AS Sku,
                           ISNULL(s.cantidad, 0) AS StockActual, ISNULL(s.cantidadMinimoAlerta, 0) AS StockMinimo,
                           c.nombre AS Categoria, pe.nombre AS Proveedor
                    FROM Productos p
                    LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
                    LEFT JOIN Categoria c ON c.id = p.categoriaID
                    LEFT JOIN Proveedores pe ON pe.id = p.proveedorID
                    WHERE (@EmpresaId IS NULL OR p.empresaID = @EmpresaId)
                    ORDER BY p.nombre;";
        return connection.Query<StockRegistro>(sql, new { EmpresaId = empresaId == 0 ? null : empresaId }).ToList();
    }

    public static List<VentaRegistro> ObtenerVentas(IConfiguration configuration, int? empresaId = null)
    {
        using var connection = CrearConexion(configuration);
        var sql = @"SELECT v.id AS VentaId, v.fecha AS Fecha, v.metodoPago AS MetodoPago, v.totalVenta AS TotalVenta,
                           d.productoID AS ProductoId, d.cantidad AS Cantidad, d.precioVenta AS PrecioUnitario,
                           p.nombre AS NombreProducto, p.sku AS Sku, c.nombre AS Categoria, pe.nombre AS Proveedor
                    FROM Ventas v
                    INNER JOIN DetalleVenta d ON d.ventaID = v.id
                    INNER JOIN Productos p ON p.id = d.productoID
                    LEFT JOIN Categoria c ON c.id = p.categoriaID
                    LEFT JOIN Proveedores pe ON pe.id = p.proveedorID
                    WHERE (@EmpresaId IS NULL OR v.empresaID = @EmpresaId)
                    ORDER BY v.fecha DESC;";
        return connection.Query<VentaRegistro>(sql, new { EmpresaId = empresaId == 0 ? null : empresaId }).ToList();
    }

    public static List<VentaRegistro> ObtenerVentasPorRangoFechas(IConfiguration configuration, DateTime fechaInicio, DateTime fechaFin, int? empresaId = null)
    {
        using var connection = CrearConexion(configuration);
        var sql = @"SELECT v.id AS VentaId, v.fecha AS Fecha, v.metodoPago AS MetodoPago, v.totalVenta AS TotalVenta,
                           d.productoID AS ProductoId, d.cantidad AS Cantidad, d.precioVenta AS PrecioUnitario,
                           p.nombre AS NombreProducto, p.sku AS Sku, c.nombre AS Categoria, pe.nombre AS Proveedor
                    FROM Ventas v
                    INNER JOIN DetalleVenta d ON d.ventaID = v.id
                    INNER JOIN Productos p ON p.id = d.productoID
                    LEFT JOIN Categoria c ON c.id = p.categoriaID
                    LEFT JOIN Proveedores pe ON pe.id = p.proveedorID
                    WHERE (@EmpresaId IS NULL OR v.empresaID = @EmpresaId)
                      AND v.fecha >= @FechaInicio AND v.fecha <= @FechaFin
                    ORDER BY v.fecha DESC;";
        return connection.Query<VentaRegistro>(sql, new { EmpresaId = empresaId == 0 ? null : empresaId, FechaInicio = fechaInicio, FechaFin = fechaFin }).ToList();
    }

    public static List<ChartPoint> ObtenerVentasDiarias(IConfiguration configuration, DateTime fechaInicio, DateTime fechaFin, int? empresaId = null)
    {
        using var connection = CrearConexion(configuration);
        var sql = @"SELECT CAST(v.fecha AS date) AS Label, COALESCE(SUM(d.precioVenta), 0) AS Valor
                    FROM DetalleVenta d
                    INNER JOIN Ventas v ON v.id = d.ventaID
                    INNER JOIN Productos p ON p.id = d.productoID
                    WHERE (@EmpresaId IS NULL OR v.empresaID = @EmpresaId)
                      AND v.fecha >= @FechaInicio AND v.fecha <= @FechaFin
                    GROUP BY CAST(v.fecha AS date)
                    ORDER BY CAST(v.fecha AS date);";
        return connection.Query<ChartPoint>(sql, new { EmpresaId = empresaId == 0 ? null : empresaId, FechaInicio = fechaInicio, FechaFin = fechaFin }).ToList();
    }

    public static List<ChartPoint> ObtenerVentasPorSemana(IConfiguration configuration, DateTime fechaInicio, DateTime fechaFin, int? empresaId = null)
    {
        using var connection = CrearConexion(configuration);
        var sql = @"SELECT DATEPART(ISO_WEEK, v.fecha) AS Semana, YEAR(v.fecha) AS Anio, COALESCE(SUM(d.precioVenta), 0) AS Valor
                    FROM DetalleVenta d
                    INNER JOIN Ventas v ON v.id = d.ventaID
                    INNER JOIN Productos p ON p.id = d.productoID
                    WHERE (@EmpresaId IS NULL OR v.empresaID = @EmpresaId)
                      AND v.fecha >= @FechaInicio AND v.fecha <= @FechaFin
                    GROUP BY DATEPART(ISO_WEEK, v.fecha), YEAR(v.fecha)
                    ORDER BY YEAR(v.fecha), DATEPART(ISO_WEEK, v.fecha);";
        return connection.Query<ChartPoint>(sql, new { EmpresaId = empresaId == 0 ? null : empresaId, FechaInicio = fechaInicio, FechaFin = fechaFin }).ToList();
    }

    public static List<ChartPoint> ObtenerGananciasPorCategoria(IConfiguration configuration, DateTime fechaInicio, DateTime fechaFin, int? empresaId = null)
    {
        using var connection = CrearConexion(configuration);
        var sql = @"SELECT c.nombre AS Label, COALESCE(SUM(d.ganancia), 0) AS Valor
                    FROM DetalleVenta d
                    INNER JOIN Ventas v ON v.id = d.ventaID
                    INNER JOIN Productos p ON p.id = d.productoID
                    LEFT JOIN Categoria c ON c.id = p.categoriaID
                    WHERE (@EmpresaId = 0 OR v.empresaID = @EmpresaId)
                      AND v.fecha >= @FechaInicio AND v.fecha <= @FechaFin
                    GROUP BY c.nombre
                    ORDER BY Valor DESC;";
        return connection.Query<ChartPoint>(sql, new { EmpresaId = empresaId == 0 ? null : empresaId, FechaInicio = fechaInicio, FechaFin = fechaFin }).ToList();
    }

    public static List<ChartPoint> ObtenerParticipacionVentas(IConfiguration configuration, DateTime fechaInicio, DateTime fechaFin, int? empresaId = null)
    {
        using var connection = CrearConexion(configuration);
        var sql = @"SELECT c.nombre AS Nombre, SUM(d.precioVenta) AS Valor
                    FROM DetalleVenta d
                    INNER JOIN Ventas v ON v.id = d.ventaID
                    INNER JOIN Productos p ON p.id = d.productoID
                    LEFT JOIN Categoria c ON c.id = p.categoriaID
                    WHERE (@EmpresaId = 0 OR v.empresaID = @EmpresaId)
                      AND v.fecha >= @FechaInicio AND v.fecha <= @FechaFin
                    GROUP BY c.nombre
                    ORDER BY Valor DESC;";
        var rows = connection.Query<(string Nombre, decimal Valor)>(sql, new { EmpresaId = empresaId == 0 ? null : empresaId, FechaInicio = fechaInicio, FechaFin = fechaFin }).ToList();
        if (rows.Count == 0) return new List<ChartPoint>();

        var total = rows.Sum(r => r.Valor);
        return rows.Select(r => new ChartPoint
        {
            Label = r.Nombre,
            Valor = total > 0 ? Math.Round((r.Valor / total) * 100m, 0, MidpointRounding.AwayFromZero) : 0m
        }).ToList();
    }

    public static async Task<bool> TienePermisoEditarEmpresaAsync(IConfiguration configuration, int usuario, int empresa)
    {
        using var connection = CrearConexion(configuration);
        return await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1)
            FROM Usuarios u
            INNER JOIN Rol r ON r.id = u.rolID
            INNER JOIN Permisos p ON p.id = r.permisoID
            WHERE u.id = @usuario AND u.empresaID = @empresa AND p.gestionarUsuarios = 1;",
            new { usuario, empresa }) == 1;
    }

    public static async Task<PermisosLectura?> ObtenerPermisosLecturaAsync(IConfiguration configuration, int usuarioId, int empresaId)
    {
        using var connection = CrearConexion(configuration);
        return await connection.QuerySingleOrDefaultAsync<PermisosLectura>(@"
            SELECT e.nombre AS NombreEmpresa,
                   CAST(CASE WHEN p.verProductos = 1 THEN 1 ELSE 0 END AS bit) AS VerProductos,
                   CAST(CASE WHEN p.verStock = 1 THEN 1 ELSE 0 END AS bit) AS VerStock,
                   CAST(CASE WHEN p.verHistorialVentas = 1 THEN 1 ELSE 0 END AS bit) AS VerVentas,
                   CAST(CASE WHEN p.verGanacia = 1 THEN 1 ELSE 0 END AS bit) AS VerGanancias
            FROM Usuarios u
            INNER JOIN Empresa e ON e.id = u.empresaID
            INNER JOIN Rol r ON r.id = u.rolID
            INNER JOIN Permisos p ON p.id = r.permisoID
            WHERE u.id = @UsuarioId
              AND u.empresaID = @EmpresaId;",
            new { UsuarioId = usuarioId, EmpresaId = empresaId });
    }

    public static async Task<ResumenStock> ObtenerResumenStockAsync(IConfiguration configuration, int empresaId)
    {
        using var connection = CrearConexion(configuration);
        return await connection.QuerySingleOrDefaultAsync<ResumenStock>(@"
            SELECT COUNT(*) AS TotalProductos,
                   SUM(CASE WHEN ISNULL(s.cantidad, 0) <= 0 THEN 1 ELSE 0 END) AS Agotados,
                   SUM(CASE WHEN ISNULL(s.cantidad, 0) > 0 AND ISNULL(s.cantidad, 0) <= ISNULL(s.cantidadMinimoAlerta, 0) THEN 1 ELSE 0 END) AS BajoMinimo
            FROM Productos p
            LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
            WHERE p.empresaID = @EmpresaId;",
            new { EmpresaId = empresaId }) ?? new ResumenStock();
    }

    public static async Task<List<ProductoLectura>> ObtenerProductosLecturaAsync(IConfiguration configuration, int empresaId, bool incluirStockBajo = true)
    {
        using var connection = CrearConexion(configuration);
        var sql = @"
            SELECT TOP (80)
                p.nombre AS Nombre,
                p.sku AS Sku,
                c.nombre AS Categoria,
                ISNULL(s.cantidad, 0) AS StockActual,
                ISNULL(s.cantidadMinimoAlerta, 0) AS StockMinimo,
                p.precioVenta AS Precio,
                p.costoCompra AS CostoCompra
            FROM Productos p
            LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
            LEFT JOIN Categoria c ON c.id = p.categoriaID
            WHERE p.empresaID = @EmpresaId
            ORDER BY CASE WHEN @IncluirStockBajo = 1 AND (ISNULL(s.cantidad, 0) <= ISNULL(s.cantidadMinimoAlerta, 0) OR ISNULL(s.cantidad, 0) = 0) THEN 0 ELSE 1 END,
                     ISNULL(s.cantidad, 0) ASC,
                     p.nombre ASC;";

        return (await connection.QueryAsync<ProductoLectura>(sql, new { EmpresaId = empresaId, IncluirStockBajo = incluirStockBajo })).ToList();
    }

    public static async Task<int> ExisteTablaVentasAsync(IConfiguration configuration)
    {
        using var connection = CrearConexion(configuration);
        return await connection.ExecuteScalarAsync<int>(@"SELECT CASE WHEN OBJECT_ID('dbo.Ventas', 'U') IS NOT NULL THEN 1 ELSE 0 END;");
    }

    public static async Task<List<VentaDiaria>> ObtenerVentasUltimos30DiasAsync(IConfiguration configuration, int empresaId)
    {
        using var connection = CrearConexion(configuration);
        return (await connection.QueryAsync<VentaDiaria>(@"
            SELECT CAST(v.fecha AS date) AS Fecha,
                   COUNT(*) AS Operaciones,
                   SUM(d.cantidad) AS Unidades,
                   SUM(d.precioVenta) AS Total,
                   SUM(d.costoCompra) AS Costo
            FROM Ventas v
            INNER JOIN DetalleVenta d ON d.ventaID = v.id
            WHERE v.empresaID = @EmpresaId
              AND v.fecha >= DATEADD(day, -30, CAST(GETDATE() AS date))
            GROUP BY CAST(v.fecha AS date)
            ORDER BY CAST(v.fecha AS date) DESC;",
            new { EmpresaId = empresaId })).ToList();
    }

    public static async Task<List<ProductoVentaIA>> ObtenerRankingProductosAsync(IConfiguration configuration, int empresaId, int? ultimosDias = null)
    {
        using var connection = CrearConexion(configuration);
        var desde = ultimosDias is > 0 ? DateTime.Now.Date.AddDays(-ultimosDias.Value) : (DateTime?)null;

        var tieneVentasAntiguas =
            ExisteColumna(connection, "Ventas", "productoId") &&
            ExisteColumna(connection, "Ventas", "cantidad") &&
            ExisteColumna(connection, "Ventas", "precio") &&
            ExisteColumna(connection, "Ventas", "costo");

        var ventasAntiguasSql = tieneVentasAntiguas
            ? @"UNION ALL
                SELECT
                    v.productoId AS ProductoId,
                    SUM(ISNULL(v.cantidad, 1)) AS Unidades,
                    SUM(ISNULL(v.precio, v.totalVenta)) AS Facturacion,
                    SUM(ISNULL(v.precio, v.totalVenta) - ISNULL(v.costo, 0)) AS Ganancia
                FROM Ventas v
                WHERE v.empresaID = @EmpresaId
                  AND (@Desde IS NULL OR v.fecha >= @Desde)
                  AND v.productoId IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM DetalleVenta d WHERE d.ventaID = v.id)
                GROUP BY v.productoId"
            : string.Empty;

        var sql = @"
            WITH Movimientos AS (
                SELECT d.productoID AS ProductoId,
                       SUM(d.cantidad) AS Unidades,
                       SUM(d.precioVenta) AS Facturacion,
                       SUM(COALESCE(d.ganancia, d.precioVenta - d.costoCompra)) AS Ganancia
                FROM DetalleVenta d
                INNER JOIN Ventas v ON v.id = d.ventaID
                WHERE v.empresaID = @EmpresaId
                  AND (@Desde IS NULL OR v.fecha >= @Desde)
                GROUP BY d.productoID
                " + ventasAntiguasSql + @"
            ),
            Totales AS (
                SELECT ProductoId,
                       SUM(Unidades) AS Unidades,
                       SUM(Facturacion) AS Facturacion,
                       SUM(Ganancia) AS Ganancia
                FROM Movimientos
                GROUP BY ProductoId
            )
            SELECT TOP (20)
                p.nombre AS Nombre,
                p.sku AS Sku,
                t.Unidades,
                t.Facturacion,
                t.Ganancia
            FROM Totales t
            INNER JOIN Productos p ON p.id = t.ProductoId
            WHERE p.empresaID = @EmpresaId
            ORDER BY t.Unidades DESC, t.Facturacion DESC;";

        return (await connection.QueryAsync<ProductoVentaIA>(sql, new { EmpresaId = empresaId, Desde = desde })).ToList();
    }

    public static async Task<bool> IAUsuarioValidoAsync(IConfiguration configuration, int usuario, int empresa)
    {
        using var connection = CrearConexion(configuration);
        return await connection.ExecuteScalarAsync<int>(@"SELECT COUNT(1) FROM Usuarios WHERE id = @Usuario AND empresaID = @Empresa;",
            new { Usuario = usuario, Empresa = empresa }) == 1;
    }

    public static async Task<List<IAChatResumen>> IAListarChatsAsync(IConfiguration configuration, int usuario, int empresa)
    {
        using var connection = CrearConexion(configuration);
        return (await connection.QueryAsync<IAChatResumen>(@"
            SELECT Id, Titulo, ActualizadoEn
            FROM IAChats
            WHERE UsuarioId = @Usuario
              AND EmpresaId = @Empresa
            ORDER BY ActualizadoEn DESC, Titulo ASC;",
            new { Usuario = usuario, Empresa = empresa })).ToList();
    }

    public static async Task<Guid> IACrearChatAsync(IConfiguration configuration, int usuario, int empresa)
    {
        var chatId = Guid.NewGuid();
        using var connection = CrearConexion(configuration);
        await connection.ExecuteAsync(@"
            INSERT INTO IAChats (Id, UsuarioId, EmpresaId, Titulo, CreadoEn, ActualizadoEn)
            VALUES (@Id, @Usuario, @Empresa, @Titulo, SYSUTCDATETIME(), SYSUTCDATETIME());",
            new { Id = chatId, Usuario = usuario, Empresa = empresa, Titulo = "Nuevo chat" });
        return chatId;
    }

    public static async Task<bool> IAExisteChatAsync(IConfiguration configuration, Guid chat, int usuario, int empresa)
    {
        using var connection = CrearConexion(configuration);
        return await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1)
            FROM IAChats
            WHERE Id = @ChatId
              AND UsuarioId = @Usuario
              AND EmpresaId = @Empresa;",
            new { ChatId = chat, Usuario = usuario, Empresa = empresa }) == 1;
    }

    public static async Task<List<IAMensaje>> IALeerChatAsync(IConfiguration configuration, Guid chat, int usuario, int empresa)
    {
        using var connection = CrearConexion(configuration);
        return (await connection.QueryAsync<IAMensaje>(@"
            SELECT m.Rol, m.Texto AS Text, m.ContenidoJson
            FROM IAMensajes m
            INNER JOIN IAChats c ON c.Id = m.ChatId
            WHERE m.ChatId = @ChatId
              AND c.UsuarioId = @Usuario
              AND c.EmpresaId = @Empresa
            ORDER BY m.Id ASC;",
            new { ChatId = chat, Usuario = usuario, Empresa = empresa })).ToList();
    }

    public static async Task GuardarIntercambioAsync(IConfiguration configuration, Guid chat, int usuario, int empresa, string pregunta, string respuesta, string? tituloSugerido, string? contenidoJson)
    {
        using var connection = CrearConexion(configuration);
        var tituloNormalizado = string.IsNullOrWhiteSpace(tituloSugerido) ? null : tituloSugerido.Trim();
        await connection.ExecuteAsync(@"
            INSERT INTO IAMensajes (ChatId, Rol, Texto)
            VALUES (@ChatId, 'user', @Pregunta);

            INSERT INTO IAMensajes (ChatId, Rol, Texto, ContenidoJson)
            VALUES (@ChatId, 'assistant', @Respuesta, @ContenidoJson);

            UPDATE IAChats
            SET ActualizadoEn = SYSUTCDATETIME(),
                Titulo = CASE 
                    WHEN Titulo = 'Nuevo chat'
                         AND @TituloSugerido IS NOT NULL
                         AND LTRIM(RTRIM(@TituloSugerido)) <> '' THEN @TituloSugerido
                    ELSE Titulo
                END
            WHERE Id = @ChatId
              AND UsuarioId = @Usuario
              AND EmpresaId = @Empresa;",
            new { ChatId = chat, Usuario = usuario, Empresa = empresa, Pregunta = pregunta, Respuesta = respuesta, ContenidoJson = contenidoJson, TituloSugerido = tituloNormalizado });
    }

    public static async Task<bool> IARenombrarChatAsync(IConfiguration configuration, Guid chat, int usuario, int empresa, string titulo)
    {
        using var connection = CrearConexion(configuration);
        return await connection.ExecuteAsync(@"
            UPDATE IAChats
            SET Titulo = @Titulo,
                ActualizadoEn = SYSUTCDATETIME()
            WHERE Id = @ChatId
              AND UsuarioId = @Usuario
              AND EmpresaId = @Empresa;",
            new { ChatId = chat, Usuario = usuario, Empresa = empresa, Titulo = titulo.Trim() }) > 0;
    }

    public static async Task<bool> IAEliminarChatAsync(IConfiguration configuration, Guid chat, int usuario, int empresa)
    {
        using var connection = CrearConexion(configuration);
        return await connection.ExecuteAsync(@"
            DELETE FROM IAChats
            WHERE Id = @ChatId
              AND UsuarioId = @Usuario
              AND EmpresaId = @Empresa;",
            new { ChatId = chat, Usuario = usuario, Empresa = empresa }) > 0;
    }

    public static async Task<bool> IAUsuarioTienePermisoAsync(IConfiguration configuration, int usuario, int empresa, string permiso)
    {
        var columna = permiso?.Trim() switch
        {
            "verProductos" => "p.verProductos",
            "crearProductos" => "p.crearProductos",
            "editarProductos" => "p.editarProductos",
            "eliminarProductos" => "p.eliminarProductos",
            "verStock" => "p.verStock",
            "verDashboard" => "p.verDashboard",
            "gestionarUsuarios" => "p.gestionarUsuarios",
            "verVentas" => "p.verHistorialVentas",
            "registrarVentas" => "p.registrarVentas",
            "verGanancias" => "p.verGanacia",
            _ => "p.verProductos"
        };

        using var connection = CrearConexion(configuration);
        var sql = $@"SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM Usuarios u
                INNER JOIN Rol r ON r.id = u.rolID
                INNER JOIN Permisos p ON p.id = r.permisoID
                WHERE u.id = @Usuario
                  AND u.empresaID = @Empresa
                  AND {columna} = 1
            ) THEN 1 ELSE 0 END;";

        return await connection.ExecuteScalarAsync<int>(sql, new { Usuario = usuario, Empresa = empresa }) == 1;
    }

    public static async Task<bool> IAPermitidoAsync(IConfiguration configuration, int usuario, int empresa, string tipo)
    {
        var permiso = tipo?.Trim() switch
        {
            "crear" => "crearProductos",
            "precio" => "editarProductos",
            "stock" => "editarProductos",
            "eliminar" => "eliminarProductos",
            _ => "verProductos"
        };

        return await IAUsuarioTienePermisoAsync(configuration, usuario, empresa, permiso);
    }

    public static async Task<bool> IAProductoExisteSkuAsync(IConfiguration configuration, int empresaId, string sku)
    {
        using var connection = CrearConexion(configuration);
        return await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1)
            FROM Productos
            WHERE empresaID = @EmpresaId
              AND LOWER(LTRIM(RTRIM(sku))) = LOWER(LTRIM(RTRIM(@Sku)));",
            new { EmpresaId = empresaId, Sku = sku.Trim() }) > 0;
    }

    public static async Task<List<IAProductoSql.Item>> IABuscarProductoPorSkuAsync(IConfiguration configuration, int empresaId, string sku, IDbTransaction? transaction = null)
    {
        using var connection = transaction == null ? CrearConexion(configuration) : transaction.Connection!;
        var sql = @"
            SELECT p.id AS Id,
                   p.nombre AS Nombre,
                   p.sku AS Sku,
                   p.precioVenta AS Precio,
                   ISNULL(s.cantidad, 0) AS Stock
            FROM Productos p
            LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
            WHERE p.empresaID = @EmpresaId
              AND LOWER(LTRIM(RTRIM(p.sku))) = LOWER(LTRIM(RTRIM(@Sku)))
            ORDER BY p.id;";

        var items = await connection.QueryAsync<IAProductoSql.Item>(sql, new { EmpresaId = empresaId, Sku = sku.Trim() }, transaction);
        return items.AsList();
    }

    public static async Task<int> IAProductoDuplicadoSkuAsync(IConfiguration configuration, int empresaId, string sku, IDbTransaction? transaction = null)
    {
        using var connection = transaction == null ? CrearConexion(configuration) : transaction.Connection!;
        return await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1)
            FROM Productos
            WHERE empresaID = @EmpresaId
              AND LOWER(LTRIM(RTRIM(sku))) = LOWER(LTRIM(RTRIM(@Sku)));",
            new { EmpresaId = empresaId, Sku = sku.Trim() }, transaction);
    }

    public static async Task<int> IACrearProductoAsync(IConfiguration configuration, int empresaId, string nombre, string sku, string? categoria, string? descripcion, decimal precio, int stock, int stockMinimo, string? proveedor, decimal costoCompra, IDbTransaction? transaction = null)
    {
        using var connection = transaction == null ? CrearConexion(configuration) : transaction.Connection!;

        var categoriaNombre = string.IsNullOrWhiteSpace(categoria) ? "Sin categoría" : categoria.Trim();
        var categoriaId = connection.QuerySingleOrDefault<int?>(@"
            SELECT TOP 1 id
            FROM Categoria
            WHERE empresaID = @EmpresaId
              AND LOWER(LTRIM(RTRIM(nombre))) = LOWER(LTRIM(RTRIM(@Nombre)))",
            new { EmpresaId = empresaId, Nombre = categoriaNombre }, transaction) ?? 0;

        if (categoriaId == 0)
        {
            categoriaId = connection.QuerySingle<int>(@"
                INSERT INTO Categoria (empresaID, nombre)
                VALUES (@EmpresaId, @Nombre);
                SELECT CAST(SCOPE_IDENTITY() AS int);",
                new { EmpresaId = empresaId, Nombre = categoriaNombre }, transaction);
        }

        var productoId = connection.QuerySingle<int>(@"
            INSERT INTO Productos (empresaID, categoriaID, proveedorID, nombre, sku, descripcion, fechaCarga, precioVenta, costoCompra, activo)
            VALUES (@EmpresaId, @CategoriaId, @ProveedorId, @Nombre, @Sku, @Descripcion, SYSUTCDATETIME(), @Precio, @CostoCompra, 1);
            SELECT CAST(SCOPE_IDENTITY() AS int);",
            new
            {
                EmpresaId = empresaId,
                CategoriaId = categoriaId,
                ProveedorId = (int?)null,
                Nombre = nombre.Trim(),
                Sku = sku.Trim(),
                Descripcion = descripcion,
                Precio = precio,
                CostoCompra = costoCompra
            }, transaction);

        connection.Execute(@"
            INSERT INTO Stock (productoID, cantidad, cantidadMinimoAlerta, activo, actualizadoEn)
            VALUES (@ProductoId, @Cantidad, @CantidadMinimoAlerta, 1, SYSUTCDATETIME());",
            new { ProductoId = productoId, Cantidad = stock, CantidadMinimoAlerta = stockMinimo }, transaction);

        return productoId;
    }

    public static async Task<bool> IAActualizarProductoPrecioAsync(IConfiguration configuration, int empresaId, int productoId, decimal precio, IDbTransaction? transaction = null)
    {
        using var connection = transaction == null ? CrearConexion(configuration) : transaction.Connection!;
        return await connection.ExecuteAsync(@"
            UPDATE Productos
            SET precioVenta = @Precio,
                costoCompra = CASE WHEN costoCompra = 0 THEN @Precio ELSE costoCompra END
            WHERE id = @ProductoId
              AND empresaID = @EmpresaId;",
            new { EmpresaId = empresaId, ProductoId = productoId, Precio = precio }, transaction) > 0;
    }

    public static async Task<bool> IAActualizarProductoStockAsync(IConfiguration configuration, int empresaId, int productoId, int stock, IDbTransaction? transaction = null)
    {
        using var connection = transaction == null ? CrearConexion(configuration) : transaction.Connection!;
        var existe = await connection.QuerySingleOrDefaultAsync<int?>(@"SELECT TOP 1 id FROM Stock WHERE productoID = @ProductoId;",
            new { ProductoId = productoId }, transaction);

        if (existe is null)
        {
            return await connection.ExecuteAsync(@"
                INSERT INTO Stock (productoID, cantidad, cantidadMinimoAlerta, activo, actualizadoEn)
                VALUES (@ProductoId, @Stock, 0, 1, SYSUTCDATETIME());",
                new { ProductoId = productoId, Stock = stock }, transaction) > 0;
        }

        return await connection.ExecuteAsync(@"
            UPDATE Stock
            SET cantidad = @Stock,
                actualizadoEn = SYSUTCDATETIME()
            WHERE productoID = @ProductoId
              AND EXISTS (SELECT 1 FROM Productos p WHERE p.id = @ProductoId AND p.empresaID = @EmpresaId);",
            new { EmpresaId = empresaId, ProductoId = productoId, Stock = stock }, transaction) > 0;
    }

    public static async Task<int> IAContarVentasProductoAsync(IConfiguration configuration, int empresaId, int productoId, IDbTransaction? transaction = null)
    {
        using var connection = transaction == null ? CrearConexion(configuration) : transaction.Connection!;
        return await connection.ExecuteScalarAsync<int>(@"
            SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM DetalleVenta dv
                    INNER JOIN Ventas v ON v.id = dv.ventaID
                    WHERE dv.productoID = @ProductoId AND v.empresaID = @EmpresaId
                ) OR EXISTS (
                    SELECT 1 FROM Ventas v
                    WHERE v.productoId = @ProductoId AND v.empresaID = @EmpresaId
                ) THEN 1 ELSE 0 END;",
            new { EmpresaId = empresaId, ProductoId = productoId }, transaction);
    }

    public static async Task<bool> IAEliminarProductoAsync(IConfiguration configuration, int empresaId, int productoId, IDbTransaction? transaction = null)
    {
        using var connection = transaction == null ? CrearConexion(configuration) : transaction.Connection!;
        return await connection.ExecuteAsync(@"DELETE FROM Productos WHERE id = @ProductoId AND empresaID = @EmpresaId;",
            new { EmpresaId = empresaId, ProductoId = productoId }, transaction) > 0;
    }

    public static async Task IAInsertarAuditoriaAsync(IConfiguration configuration, int usuario, int empresa, string accion, int productoId, string antes, string despues, IDbTransaction? transaction = null)
    {
        using var connection = transaction == null ? CrearConexion(configuration) : transaction.Connection!;
        await connection.ExecuteAsync(@"
            INSERT INTO IAAuditoria (UsuarioId, EmpresaId, Accion, ProductoId, Antes, Despues, Fecha)
            VALUES (@UsuarioId, @EmpresaId, @Accion, @ProductoId, @Antes, @Despues, SYSUTCDATETIME());",
            new { UsuarioId = usuario, EmpresaId = empresa, Accion = accion, ProductoId = productoId, Antes = antes, Despues = despues }, transaction);
    }

    public static int? ObtenerUsuarioLogueadoID(HttpContext httpContext)
    {
        return httpContext.Session.GetInt32("UsuarioID");
    }

    public static int ObtenerEmpresaIdActual(IConfiguration configuration, HttpContext httpContext)
    {
        var empresaId = httpContext.Session.GetInt32("EmpresaID");
        if (empresaId.HasValue && empresaId.Value > 0)
            return empresaId.Value;

        var usuarioId = httpContext.Session.GetInt32("UsuarioID");
        if (!usuarioId.HasValue || usuarioId.Value <= 0)
            return 0;

        using var connection = CrearConexion(configuration);
        var empresa = connection.QuerySingleOrDefault<int?>(@"SELECT empresaID FROM Usuarios WHERE id = @UsuarioId;",
            new { UsuarioId = usuarioId.Value });

        if (empresa.HasValue && empresa.Value > 0)
        {
            httpContext.Session.SetInt32("EmpresaID", empresa.Value);
            return empresa.Value;
        }

        return 0;
    }

    public static bool IniciarSesion(string mail, string contrasena, HttpContext httpContext, IConfiguration configuration)
    {
        var correo = (mail ?? string.Empty).Trim();
        var clave = (contrasena ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(clave))
            return false;

        using var connection = CrearConexion(configuration);
        var usuario = connection.QuerySingleOrDefault<dynamic>(@"
            SELECT TOP 1 u.id AS Id,
                         u.nombreUsuario AS NombreUsuario,
                         u.empresaID AS EmpresaID,
                         e.nombre AS NombreEmpresa
            FROM Usuarios u
            LEFT JOIN Empresa e ON e.id = u.empresaID
            WHERE LOWER(LTRIM(RTRIM(u.mail))) = LOWER(LTRIM(RTRIM(@Mail)))
              AND u.contrasena = @Contrasena;",
            new { Mail = correo, Contrasena = clave });

        if (usuario == null)
            return false;

        httpContext.Session.SetInt32("UsuarioID", (int)usuario.Id);
        httpContext.Session.SetString("NombreUsuario", usuario.NombreUsuario ?? string.Empty);
        httpContext.Session.SetInt32("EmpresaID", (int)usuario.EmpresaID);
        httpContext.Session.SetString("NombreEmpresa", usuario.NombreEmpresa ?? string.Empty);
        return true;
    }

    public static bool Registrarse(Usuario usuario, IConfiguration configuration)
    {
        if (usuario == null)
            return false;

        var nombreUsuario = (usuario.NombreUsuario ?? string.Empty).Trim();
        var mail = (usuario.Mail ?? string.Empty).Trim();
        var nombreEmpresa = (usuario.NombreEmpresa ?? string.Empty).Trim();
        var contrasena = (usuario.Contrasena ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(nombreUsuario) || string.IsNullOrWhiteSpace(mail) || string.IsNullOrWhiteSpace(nombreEmpresa) || string.IsNullOrWhiteSpace(contrasena))
            return false;

        using var connection = CrearConexion(configuration);

        if (connection.QuerySingleOrDefault<int?>(@"SELECT TOP 1 id FROM Usuarios WHERE LOWER(LTRIM(RTRIM(mail))) = LOWER(LTRIM(RTRIM(@Mail)));",
                new { Mail = mail }) != null)
            return false;

        var empresaId = usuario.EmpresaID > 0
            ? usuario.EmpresaID
            : connection.QuerySingleOrDefault<int?>(@"SELECT TOP 1 id FROM Empresa WHERE LOWER(LTRIM(RTRIM(nombre))) = LOWER(LTRIM(RTRIM(@Nombre)));",
                new { Nombre = nombreEmpresa }) ?? 0;

        if (empresaId <= 0)
        {
            empresaId = connection.QuerySingle<int>(@"
                INSERT INTO Empresa (nombre)
                VALUES (@Nombre);
                SELECT CAST(SCOPE_IDENTITY() AS int);",
                new { Nombre = nombreEmpresa });
        }

        var rolId = usuario.RolID > 0 ? usuario.RolID : 1;
        connection.Execute(@"
            INSERT INTO Usuarios (empresaID, rolID, nombreUsuario, mail, contrasena, numeroTelefono)
            VALUES (@EmpresaID, @RolID, @NombreUsuario, @Mail, @Contrasena, @NumeroTelefono);",
            new
            {
                EmpresaID = empresaId,
                RolID = rolId,
                NombreUsuario = nombreUsuario,
                Mail = mail,
                Contrasena = contrasena,
                NumeroTelefono = usuario.NumeroTelefono
            });

        return true;
    }

    public static void CerrarSesion(HttpContext httpContext)
    {
        httpContext.Session.Remove("UsuarioID");
        httpContext.Session.Remove("EmpresaID");
        httpContext.Session.Remove("NombreUsuario");
        httpContext.Session.Remove("NombreEmpresa");
        httpContext.Session.Remove("UltimaVentaTimestamp");
    }

    public static bool TieneProductos(IConfiguration configuration, int empresaId)
    {
        using var connection = CrearConexion(configuration);
        return connection.ExecuteScalar<int>(@"SELECT COUNT(1) FROM Productos WHERE empresaID = @EmpresaId;",
            new { EmpresaId = empresaId }) > 0;
    }

    public static bool EliminarProducto(IConfiguration configuration, int id)
    {
        using var connection = CrearConexion(configuration);
        return connection.Execute(@"DELETE FROM Productos WHERE id = @Id;", new { Id = id }) > 0;
    }

    public static bool AgregarStock(IConfiguration configuration, int id, int cantidad, decimal precioUnitario)
    {
        if (cantidad <= 0)
            return false;

        using var connection = CrearConexion(configuration);
        var existe = connection.QuerySingleOrDefault<int?>(@"SELECT TOP 1 id FROM Stock WHERE productoID = @ProductoId;",
            new { ProductoId = id });

        if (existe is null)
        {
            return connection.Execute(@"
                INSERT INTO Stock (productoID, cantidad, cantidadMinimoAlerta, activo, actualizadoEn)
                VALUES (@ProductoId, @Cantidad, 0, 1, SYSUTCDATETIME());",
                new { ProductoId = id, Cantidad = cantidad }) > 0;
        }

        return connection.Execute(@"
            UPDATE Stock
            SET cantidad = cantidad + @Cantidad,
                actualizadoEn = SYSUTCDATETIME()
            WHERE productoID = @ProductoId;",
            new { ProductoId = id, Cantidad = cantidad }) > 0;
    }

    public static DashboardSummary ObtenerDashboard(IConfiguration configuration, string range, int? empresaId = null, string? nombreProducto = null, string? sku = null, string? categoria = null, DateTime? fechaInicio = null, DateTime? fechaFin = null)
    {
        using var connection = CrearConexion(configuration);

        var empresaFiltro = empresaId == 0 ? null : empresaId;
        var totalProductos = connection.QuerySingle<int>(@"SELECT COUNT(1) FROM Productos WHERE (@EmpresaId IS NULL OR empresaID = @EmpresaId);",
            new { EmpresaId = empresaFiltro });

        var productosPocoStock = connection.QuerySingle<int>(@"
            SELECT COUNT(1)
            FROM Productos p
            LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
            WHERE (@EmpresaId IS NULL OR p.empresaID = @EmpresaId)
              AND (ISNULL(s.cantidad, 0) <= ISNULL(s.cantidadMinimoAlerta, 0) OR ISNULL(s.cantidad, 0) = 0);",
            new { EmpresaId = empresaFiltro });

        var productosSinStock = connection.QuerySingle<int>(@"
            SELECT COUNT(1)
            FROM Productos p
            LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
            WHERE (@EmpresaId IS NULL OR p.empresaID = @EmpresaId)
              AND ISNULL(s.cantidad, 0) = 0;",
            new { EmpresaId = empresaFiltro });

        var ventasMes = connection.QuerySingle<decimal?>(@"
            SELECT COALESCE(SUM(totalVenta), 0)
            FROM Ventas
            WHERE (@EmpresaId IS NULL OR empresaID = @EmpresaId)
              AND fecha >= DATEADD(month, DATEDIFF(month, 0, GETDATE()), 0)
              AND fecha < DATEADD(month, DATEDIFF(month, 0, GETDATE()) + 1, 0);",
            new { EmpresaId = empresaFiltro }) ?? 0m;

        var ventasTotales = connection.QuerySingle<int>(@"SELECT COUNT(1) FROM Ventas WHERE (@EmpresaId IS NULL OR empresaID = @EmpresaId);",
            new { EmpresaId = empresaFiltro });

        return new DashboardSummary
        {
            TotalProductos = totalProductos,
            ProductosPocoStock = productosPocoStock,
            ProductosSinStock = productosSinStock,
            VentasTotales = ventasTotales,
            TotalVentasMes = ventasMes,
            VentaPromedioMes = ventasMes > 0 && ventasTotales > 0 ? ventasMes / ventasTotales : 0m,
            MesActualLabel = DateTime.Now.ToString("MMMM", System.Globalization.CultureInfo.GetCultureInfo("es-AR"))
        };
    }

    public static async Task<AnalisisVentasGananciasData> ObtenerAnalisisVentasGananciasAsync(IDbConnection connection, int empresaId, DateTime? fechaInicio = null, DateTime? fechaFin = null)
    {
        var inicio = fechaInicio ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var fin = fechaFin ?? DateTime.Today;

        var resumen = await connection.QuerySingleOrDefaultAsync(@"
            SELECT
                COALESCE(SUM(CASE WHEN v.fecha >= @FechaInicio AND v.fecha <= @FechaFin THEN v.totalVenta ELSE 0 END), 0) AS VentasTotales,
                COALESCE(SUM(CASE WHEN v.fecha >= @FechaInicio AND v.fecha <= @FechaFin THEN COALESCE(d.costoCompra, 0) ELSE 0 END), 0) AS GastosTotales,
                COUNT(CASE WHEN v.fecha >= @FechaInicio AND v.fecha <= @FechaFin THEN 1 END) AS VentasTotalesCantidad
            FROM Ventas v
            LEFT JOIN DetalleVenta d ON d.ventaID = v.id
            WHERE v.empresaID = @EmpresaId;",
            new { EmpresaId = empresaId, FechaInicio = inicio, FechaFin = fin });

        var ventasDiarias = (await connection.QueryAsync<ChartPoint>(@"
            SELECT CAST(v.fecha AS date) AS Label,
                   COALESCE(SUM(COALESCE(d.precioVenta, v.totalVenta)), 0) AS Valor
            FROM Ventas v
            LEFT JOIN DetalleVenta d ON d.ventaID = v.id
            WHERE v.empresaID = @EmpresaId
              AND v.fecha >= @FechaInicio AND v.fecha <= @FechaFin
            GROUP BY CAST(v.fecha AS date)
            ORDER BY CAST(v.fecha AS date);",
            new { EmpresaId = empresaId, FechaInicio = inicio, FechaFin = fin })).ToList();

        var ventaPorSemana = (await connection.QueryAsync<ChartPoint>(@"
            SELECT DATEPART(ISO_WEEK, v.fecha) AS Label,
                   COALESCE(SUM(COALESCE(d.precioVenta, v.totalVenta)), 0) AS Valor
            FROM Ventas v
            LEFT JOIN DetalleVenta d ON d.ventaID = v.id
            WHERE v.empresaID = @EmpresaId
              AND v.fecha >= @FechaInicio AND v.fecha <= @FechaFin
            GROUP BY DATEPART(ISO_WEEK, v.fecha)
            ORDER BY DATEPART(ISO_WEEK, v.fecha);",
            new { EmpresaId = empresaId, FechaInicio = inicio, FechaFin = fin })).ToList();

        var gananciaPorCategoria = (await connection.QueryAsync<ChartPoint>(@"
            SELECT c.nombre AS Label,
                   COALESCE(SUM(COALESCE(d.ganancia, d.precioVenta - d.costoCompra)), 0) AS Valor
            FROM DetalleVenta d
            INNER JOIN Ventas v ON v.id = d.ventaID
            INNER JOIN Productos p ON p.id = d.productoID
            LEFT JOIN Categoria c ON c.id = p.categoriaID
            WHERE v.empresaID = @EmpresaId
              AND v.fecha >= @FechaInicio AND v.fecha <= @FechaFin
            GROUP BY c.nombre
            ORDER BY Valor DESC;",
            new { EmpresaId = empresaId, FechaInicio = inicio, FechaFin = fin })).ToList();

        var ventasTotales = resumen?.VentasTotales ?? 0m;
        var gastosTotales = resumen?.GastosTotales ?? 0m;
        var cantidad = resumen?.VentasTotalesCantidad ?? 0;
        var ganancia = ventasTotales - gastosTotales;

        return new AnalisisVentasGananciasData
        {
            VentasTotales = ventasTotales,
            GastosTotales = gastosTotales,
            VentaPromedio = cantidad > 0 ? ventasTotales / cantidad : 0m,
            Ganancias = ganancia,
            VentasTotalesCantidad = cantidad,
            VentasDiarias = ventasDiarias,
            VentaPorSemana = ventaPorSemana,
            GananciaPorCategoria = gananciaPorCategoria,
            MargenNeto = ventasTotales > 0 ? (ganancia / ventasTotales) * 100m : 0m,
            FormulaGanancia = ganancia
        };
    }

    public static async Task<AnalisisProductoData> ObtenerAnalisisProductoAsync(IDbConnection connection, int empresaId, DateTime? fechaInicio = null, DateTime? fechaFin = null)
    {
        var inicio = fechaInicio ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var fin = fechaFin ?? DateTime.Today;

        var productos = (await connection.QueryAsync<AnalisisProductoRow>(@"
            SELECT p.id AS Id,
                   p.nombre AS Nombre,
                   p.sku AS Sku,
                   COALESCE(c.nombre, 'Sin categoría') AS Categoria,
                   p.precioVenta AS Precio,
                   p.costoCompra AS CostoCompra,
                   ISNULL(s.cantidad, 0) AS StockActual,
                   ISNULL(s.cantidadMinimoAlerta, 0) AS StockMinimo
            FROM Productos p
            LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
            LEFT JOIN Categoria c ON c.id = p.categoriaID
            WHERE p.empresaID = @EmpresaId
            ORDER BY p.nombre;",
            new { EmpresaId = empresaId })).ToList();

        var ventasPorProducto = (await connection.QueryAsync<AnalisisVentaProductoRow>(@"
            SELECT d.productoID AS ProductoId,
                   SUM(d.cantidad) AS CantidadVendida,
                   SUM(d.precioVenta) AS VentasTotales,
                   SUM(d.costoCompra) AS GastoTotal,
                   SUM(COALESCE(d.ganancia, d.precioVenta - d.costoCompra)) AS GananciaTotal
            FROM DetalleVenta d
            INNER JOIN Ventas v ON v.id = d.ventaID
            WHERE v.empresaID = @EmpresaId
              AND v.fecha >= @FechaInicio AND v.fecha <= @FechaFin
            GROUP BY d.productoID;",
            new { EmpresaId = empresaId, FechaInicio = inicio, FechaFin = fin })).ToList();

        var ventasDict = ventasPorProducto.ToDictionary(x => x.ProductoId, x => x);

        var productosConVenta = productos.Select(p =>
        {
            var venta = ventasDict.TryGetValue(p.Id, out var agregado) ? agregado : null;
            return new AnalisisProductoRow
            {
                Id = p.Id,
                Nombre = p.Nombre,
                Sku = p.Sku,
                Categoria = p.Categoria,
                Precio = p.Precio,
                CostoCompra = p.CostoCompra,
                StockActual = p.StockActual,
                StockMinimo = p.StockMinimo,
                CantidadVendida = venta?.CantidadVendida ?? 0
            };
        }).ToList();

        return new AnalisisProductoData
        {
            Productos = productosConVenta,
            VentasPorProducto = ventasPorProducto,
            VentasPorMes = new List<dynamic>(),
            InteresCompraPorMes = new List<dynamic>(),
            VentasSemana = new List<dynamic>()
        };
    }

    public static async Task<List<StockAlertaItem>> ObtenerAlertasStockAsync(IConfiguration configuration, int empresaId)
    {
        using var connection = CrearConexion(configuration);
        return (await connection.QueryAsync<StockAlertaItem>(@"
            SELECT p.nombre AS Nombre,
                   p.sku AS Sku,
                   ISNULL(s.cantidad, 0) AS Stock,
                   ISNULL(s.cantidadMinimoAlerta, 0) AS Minimo
            FROM Productos p
            LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
            WHERE p.empresaID = @EmpresaId
            ORDER BY p.nombre;",
            new { EmpresaId = empresaId })).ToList();
    }

    public static async Task<List<ProductoVentaIA>> ObtenerRankingProductosUltimos30DiasAsync(IConfiguration configuration, int empresaId)
    {
        return await ObtenerRankingProductosAsync(configuration, empresaId, 30);
    }

    public sealed class PermisosLectura
    {
        public string NombreEmpresa { get; set; } = string.Empty;
        public bool VerProductos { get; set; }
        public bool VerStock { get; set; }
        public bool VerVentas { get; set; }
        public bool VerGanancias { get; set; }
    }

    public sealed class ResumenStock
    {
        public int TotalProductos { get; set; }
        public int Agotados { get; set; }
        public int BajoMinimo { get; set; }
    }

    public sealed class ProductoLectura
    {
        public string Nombre { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
        public decimal Precio { get; set; }
        public decimal CostoCompra { get; set; }
    }

    public sealed class VentaDiaria
    {
        public DateTime Fecha { get; set; }
        public int Operaciones { get; set; }
        public int Unidades { get; set; }
        public decimal Total { get; set; }
        public decimal Costo { get; set; }
    }

    public sealed class ProductoVentaIA
    {
        public string Nombre { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int Unidades { get; set; }
        public decimal Facturacion { get; set; }
        public decimal Ganancia { get; set; }
    }

    public sealed class IAChatResumen
    {
        public Guid Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public DateTime ActualizadoEn { get; set; }
    }

    public sealed class IAMensaje
    {
        public int Id { get; set; }
        public string Rol { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string? ContenidoJson { get; set; }
    }

    public sealed class AnalisisVentasGananciasData
    {
        public decimal VentasTotales { get; set; }
        public decimal GastosTotales { get; set; }
        public decimal VentaPromedio { get; set; }
        public decimal Ganancias { get; set; }
        public int VentasTotalesCantidad { get; set; }
        public List<ChartPoint> VentasDiarias { get; set; } = new();
        public List<ChartPoint> VentaPorSemana { get; set; } = new();
        public List<ChartPoint> GananciaPorCategoria { get; set; } = new();
        public decimal MargenNeto { get; set; }
        public decimal FormulaGanancia { get; set; }
    }

    public sealed class AnalisisProductoRow
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public decimal CostoCompra { get; set; }
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
        public int CantidadVendida { get; set; }
    }

    public sealed class AnalisisVentaProductoRow
    {
        public int ProductoId { get; set; }
        public int CantidadVendida { get; set; }
        public decimal VentasTotales { get; set; }
        public decimal GastoTotal { get; set; }
        public decimal GananciaTotal { get; set; }
    }

    public sealed class AnalisisProductoData
    {
        public List<AnalisisProductoRow> Productos { get; set; } = new();
        public List<AnalisisVentaProductoRow> VentasPorProducto { get; set; } = new();
        public List<dynamic> VentasPorMes { get; set; } = new();
        public List<dynamic> InteresCompraPorMes { get; set; } = new();
        public List<dynamic> VentasSemana { get; set; } = new();
    }

    public sealed class StockAlertaItem
    {
        public string Nombre { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int Stock { get; set; }
        public int Minimo { get; set; }
    }

    public sealed class AjustesDatos
    {
        public string NombreUsuario { get; set; } = string.Empty;
        public string Mail { get; set; } = string.Empty;
        public string? NumeroTelefono { get; set; }
        public string NombreEmpresa { get; set; } = string.Empty;
        public bool PuedeEditarEmpresa { get; set; }
    }

    public sealed class DatosRepetidosAjustes
    {
        public int Mail { get; set; }
        public int Nombre { get; set; }
    }
}
