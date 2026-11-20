using System.Data;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using System.Globalization;
using Quantitary.Services;

namespace Quantitary.Models;

public static class BD
{
    private static string NormalizarMail(string? mail)
    {
        return (mail ?? string.Empty).Trim();
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

    private static bool TieneColumna(IConfiguration configuration, string nombreTabla, string nombreColumna)
    {
        using var connection = CrearConexion(configuration);
        connection.Open();

        var sql = @"SELECT CASE WHEN EXISTS (
                        SELECT 1
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_SCHEMA = 'dbo'
                          AND TABLE_NAME = @NombreTabla
                          AND COLUMN_NAME = @NombreColumna) THEN 1 ELSE 0 END;";

        return connection.QuerySingle<int>(sql, new { NombreTabla = nombreTabla, NombreColumna = nombreColumna }) == 1;
    }

    private static bool UsaEsquemaNormalizado(IConfiguration configuration)
    {
        return TieneColumna(configuration, "Productos", "nombre")
            && TieneColumna(configuration, "Productos", "precioVenta")
            && TieneColumna(configuration, "Productos", "categoriaID")
            && TieneColumna(configuration, "Productos", "activo")
            && TieneColumna(configuration, "Stock", "cantidadMinimoAlerta")
            && TieneColumna(configuration, "Proveedores", "nombre");
    }

    public static List<string> ObtenerCategorias(IConfiguration configuration, int? empresaId = null)
    {
        using var connection = CrearConexion(configuration);

        if (!UsaEsquemaNormalizado(configuration))
        {
            var sqlLegacy = @"SELECT DISTINCT CAST(categoria AS varchar(100)) AS categoria
                             FROM Productos
                             WHERE (@EmpresaId IS NULL OR empresaID = @EmpresaId)
                               AND LTRIM(RTRIM(COALESCE(categoria, ''))) <> ''
                             ORDER BY categoria";

            return connection.Query<string>(sqlLegacy, new { EmpresaId = empresaId == 0 ? null : empresaId }).ToList();
        }

        var sql = @"SELECT DISTINCT c.nombre AS categoria
                    FROM Productos p
                    INNER JOIN Categoria c ON c.id = p.categoriaID
                    WHERE (@EmpresaId IS NULL OR p.empresaID = @EmpresaId)
                      AND LTRIM(RTRIM(c.nombre)) <> ''
                    ORDER BY c.nombre";
        return connection.Query<string>(sql, new { EmpresaId = empresaId == 0 ? null : empresaId }).ToList();
    }

    public static List<Producto> ObtenerProductos(IConfiguration configuration, string? sku = null, string? categoria = null, bool? pocoStock = null, decimal? precioMinimo = null, string? orden = null, int? empresaId = null)
    {
        var parametros = new DynamicParameters();
        parametros.Add("@EmpresaId", empresaId == 0 ? null : empresaId);
        parametros.Add("@Sku", string.IsNullOrWhiteSpace(sku) ? null : sku.Trim());
        parametros.Add("@Categoria", string.IsNullOrWhiteSpace(categoria) ? null : categoria.Trim());
        parametros.Add("@PocoStock", pocoStock == true ? 1 : 0);
        parametros.Add("@PrecioMinimo", precioMinimo.HasValue && precioMinimo.Value > 0 ? precioMinimo.Value : null);
        parametros.Add("@Orden", string.IsNullOrWhiteSpace(orden) ? "nombre_asc" : orden.Trim());

        using var connection = CrearConexion(configuration);

        if (!UsaEsquemaNormalizado(configuration))
        {
            var sqlLegacy = @"
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
                ORDER BY
                  CASE WHEN @Orden = 'nombre_asc' THEN p.nombre END ASC,
                  CASE WHEN @Orden = 'nombre_desc' THEN p.nombre END DESC,
                  CASE WHEN @Orden = 'precio_asc' THEN COALESCE(p.precioVenta, p.precio, 0) END ASC,
                  CASE WHEN @Orden = 'precio_desc' THEN COALESCE(p.precioVenta, p.precio, 0) END DESC,
                  CASE WHEN @Orden = 'stock_asc' THEN COALESCE(p.stockActual, 0) END ASC,
                  CASE WHEN @Orden = 'stock_desc' THEN COALESCE(p.stockActual, 0) END DESC,
                  CASE WHEN @Orden = 'sku_asc' THEN p.sku END ASC,
                  CASE WHEN @Orden = 'sku_desc' THEN p.sku END DESC;";
            return connection.Query<Producto>(sqlLegacy, parametros).ToList();
        }

        var sql = @"
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
            ORDER BY
              CASE WHEN @Orden = 'nombre_asc' THEN p.nombre END ASC,
              CASE WHEN @Orden = 'nombre_desc' THEN p.nombre END DESC,
              CASE WHEN @Orden = 'precio_asc' THEN p.precioVenta END ASC,
              CASE WHEN @Orden = 'precio_desc' THEN p.precioVenta END DESC,
              CASE WHEN @Orden = 'stock_asc' THEN ISNULL(s.cantidad, 0) END ASC,
              CASE WHEN @Orden = 'stock_desc' THEN ISNULL(s.cantidad, 0) END DESC,
              CASE WHEN @Orden = 'sku_asc' THEN p.sku END ASC,
              CASE WHEN @Orden = 'sku_desc' THEN p.sku END DESC;
        ";

        return connection.Query<Producto>(sql, parametros).ToList();
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
                WHERE p.id = @Id", new { Id = id });
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
            WHERE p.id = @Id", new { Id = id });
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

        if (categoriaId > 0)
        {
            return categoriaId;
        }

        return connection.QuerySingle<int>(@"
            INSERT INTO Categoria (empresaID, nombre)
            VALUES (@EmpresaId, @Nombre);
            SELECT CAST(SCOPE_IDENTITY() AS int);",
            new { EmpresaId = empresaId, Nombre = nombre }, transaction);
    }

    private static int? ObtenerOCrearProveedorId(IDbConnection connection, int empresaId, string? proveedorNombre, IDbTransaction? transaction = null)
    {
        if (string.IsNullOrWhiteSpace(proveedorNombre))
        {
            return null;
        }

        var nombre = proveedorNombre.Trim();
        var proveedorId = connection.QuerySingleOrDefault<int?>(@"
            SELECT TOP 1 id
            FROM Proveedores
            WHERE empresaID = @EmpresaId
              AND LOWER(LTRIM(RTRIM(nombre))) = LOWER(LTRIM(RTRIM(@Nombre)))
            ORDER BY id;",
            new { EmpresaId = empresaId, Nombre = nombre }, transaction);

        if (proveedorId.HasValue)
        {
            return proveedorId.Value;
        }

        return connection.QuerySingle<int>(@"
            INSERT INTO Proveedores (empresaID, nombre, costoEnvio)
            VALUES (@EmpresaId, @Nombre, 0);
            SELECT CAST(SCOPE_IDENTITY() AS int);",
            new { EmpresaId = empresaId, Nombre = nombre }, transaction);
    }

    public static Producto AgregarProducto(IConfiguration configuration, Producto producto)
    {
        if (string.IsNullOrWhiteSpace(producto.Nombre) || string.IsNullOrWhiteSpace(producto.Sku))
        {
            throw new ArgumentException("El nombre y el SKU son obligatorios.");
        }

        producto.Nombre = producto.Nombre.Trim();
        producto.Sku = producto.Sku.Trim();
        producto.Proveedor = (producto.Proveedor ?? string.Empty).Trim();
        producto.Categoria = string.IsNullOrWhiteSpace(producto.Categoria) ? "Sin categoría" : producto.Categoria.Trim();

        if (producto.PrecioVenta <= 0)
        {
            throw new ArgumentException($"El producto '{producto.Nombre}' tiene un precio inválido.");
        }

        using var connection = CrearConexion(configuration);

        producto.CategoriaID = ObtenerOCrearCategoriaId(connection, producto.EmpresaID, producto.Categoria);
        producto.ProveedorID = ObtenerOCrearProveedorId(connection, producto.EmpresaID, producto.Proveedor);

        if (producto.Id > 0)
        {
            var idDuplicado = connection.ExecuteScalar<int>(@"
                SELECT COUNT(1)
                FROM Productos
                WHERE id = @Id AND empresaID = @EmpresaID;",
                new { producto.Id, producto.EmpresaID });

            if (idDuplicado > 0)
            {
                throw new ArgumentException($"Ya existe un producto con el ID {producto.Id}. No se permite ingresar IDs duplicados.");
            }
        }

        var skuDuplicado = connection.ExecuteScalar<int>(@"
            SELECT COUNT(1)
            FROM Productos
            WHERE empresaID = @EmpresaID
              AND LOWER(LTRIM(RTRIM(sku))) = LOWER(LTRIM(RTRIM(@Sku)));",
            new { producto.EmpresaID, producto.Sku });

        if (skuDuplicado > 0)
        {
            throw new ArgumentException($"Ya existe un producto con el SKU '{producto.Sku}'.");
        }

        var sql = @"INSERT INTO Productos (empresaID, categoriaID, proveedorID, nombre, sku, descripcion, fechaCarga, precioVenta, costoCompra, activo)
                    VALUES (@EmpresaID, @CategoriaID, @ProveedorID, @Nombre, @Sku, @Descripcion, @FechaCarga, @PrecioVenta, @CostoCompra, @Activo);
                    SELECT CAST(SCOPE_IDENTITY() as int);";

        producto.Id = connection.QuerySingle<int>(sql, producto);

        var stockSql = @"INSERT INTO Stock (productoID, cantidad, cantidadMinimoAlerta, activo, actualizadoEn)
                        VALUES (@ProductoId, @Cantidad, @CantidadMinimoAlerta, @Activo, @ActualizadoEn);";

        connection.Execute(stockSql, new
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
        {
            throw new ArgumentException("El nombre y el SKU son obligatorios.");
        }

        producto.Nombre = producto.Nombre.Trim();
        producto.Sku = producto.Sku.Trim();
        producto.Proveedor = (producto.Proveedor ?? string.Empty).Trim();
        producto.Categoria = string.IsNullOrWhiteSpace(producto.Categoria) ? "Sin categoría" : producto.Categoria.Trim();

        if (producto.PrecioVenta <= 0)
        {
            throw new ArgumentException($"El producto '{producto.Nombre}' tiene un precio inválido.");
        }

        using var connection = CrearConexion(configuration);

        var empresaId = connection.ExecuteScalar<int?>(@"
            SELECT empresaID
            FROM Productos
            WHERE id = @Id;",
            new { Id = id });

        if (!empresaId.HasValue)
        {
            return false;
        }

        if (producto.CostoCompra <= 0)
        {
            producto.CostoCompra = connection.ExecuteScalar<decimal?>(@"
                SELECT costoCompra
                FROM Productos
                WHERE id = @Id;",
                new { Id = id }) ?? 0;
        }

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
        {
            throw new ArgumentException($"No se puede guardar: ya existe otro producto con el SKU '{producto.Sku}'.");
        }

        var sql = @"UPDATE Productos
                    SET nombre = @Nombre,
                        sku = @Sku,
                        categoriaID = @CategoriaID,
                        proveedorID = @ProveedorID,
                        descripcion = @Descripcion,
                        precioVenta = @PrecioVenta,
                        costoCompra = @CostoCompra,
                        activo = @Activo
                    WHERE id = @Id";

        var filasAfectadas = connection.Execute(sql, new
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

        var filasStock = connection.Execute(@"UPDATE Stock
                            SET cantidad = @Cantidad,
                                cantidadMinimoAlerta = @CantidadMinimoAlerta,
                                activo = 1,
                                actualizadoEn = @ActualizadoEn
                            WHERE productoID = @ProductoId",
            new
            {
                Cantidad = producto.StockActual,
                CantidadMinimoAlerta = producto.StockMinimo,
                ActualizadoEn = DateTime.Now,
                ProductoId = id
            });

        if (filasStock == 0)
        {
            connection.Execute(@"
                INSERT INTO Stock (productoID, cantidad, cantidadMinimoAlerta, activo, actualizadoEn)
                VALUES (@ProductoId, @Cantidad, @CantidadMinimoAlerta, 1, @ActualizadoEn);",
                new
                {
                    Cantidad = producto.StockActual,
                    CantidadMinimoAlerta = producto.StockMinimo,
                    ActualizadoEn = DateTime.Now,
                    ProductoId = id
                });
        }

        return filasAfectadas > 0;
    }

    public static bool EliminarProducto(IConfiguration configuration, int id)
    {
        using var connection = CrearConexion(configuration);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        connection.Execute("DELETE FROM Stock WHERE productoID = @Id", new { Id = id }, transaction);
        var filas = connection.Execute("DELETE FROM Productos WHERE id = @Id", new { Id = id }, transaction);

        transaction.Commit();
        return filas > 0;
    }

    public static bool AgregarStock(IConfiguration configuration, int id, int cantidad, decimal precioUnitario)
    {
        using var connection = CrearConexion(configuration);
        var existe = connection.QuerySingleOrDefault<int?>("SELECT TOP 1 id FROM Stock WHERE productoID = @Id", new { Id = id });

        if (existe.HasValue)
        {
            return connection.Execute(@"UPDATE Stock
                                        SET cantidad = cantidad + @Cantidad,
                                            actualizadoEn = @ActualizadoEn
                                        WHERE productoID = @Id",
                new { Id = id, Cantidad = cantidad, ActualizadoEn = DateTime.Now }) > 0;
        }

        return connection.Execute(@"INSERT INTO Stock (productoID, cantidad, cantidadMinimoAlerta, activo, actualizadoEn)
                                    VALUES (@Id, @Cantidad, 0, 1, @ActualizadoEn)",
            new { Id = id, Cantidad = cantidad, ActualizadoEn = DateTime.Now }) > 0;
    }

    public static int ObtenerTotalProductos(IConfiguration configuration, int? empresaId = null)
    {
        using var connection = CrearConexion(configuration);
        return connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Productos WHERE (@EmpresaId IS NULL OR empresaID = @EmpresaId)",
            new { EmpresaId = empresaId == 0 ? null : empresaId });
    }

    public static int ObtenerProductosConStockBajo(IConfiguration configuration, int? empresaId = null)
    {
        using var connection = CrearConexion(configuration);
        return connection.ExecuteScalar<int>(
            @"SELECT COUNT(*)
              FROM Productos p
              LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
              WHERE (@EmpresaId IS NULL OR p.empresaID = @EmpresaId)
                AND (ISNULL(s.cantidad, 0) <= ISNULL(s.cantidadMinimoAlerta, 0) OR ISNULL(s.cantidad, 0) = 0)",
            new { EmpresaId = empresaId == 0 ? null : empresaId });
    }

    public static int ObtenerProductosSinStock(IConfiguration configuration, int? empresaId = null)
    {
        using var connection = CrearConexion(configuration);
        return connection.ExecuteScalar<int>(
            @"SELECT COUNT(*)
              FROM Productos p
              LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
              WHERE (@EmpresaId IS NULL OR p.empresaID = @EmpresaId)
                AND ISNULL(s.cantidad, 0) <= 0",
            new { EmpresaId = empresaId == 0 ? null : empresaId });
    }

    public static bool TieneProductos(IConfiguration configuration, int empresaId)
    {
        using var connection = CrearConexion(configuration);
        return connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Productos WHERE empresaID = @EmpresaId",
            new { EmpresaId = empresaId }) > 0;
    }

    public static DashboardSummary ObtenerDashboard(IConfiguration configuration, string range, int? empresaId = null, string? nombreProducto = null, string? sku = null, string? categoria = null, DateTime? fechaInicio = null, DateTime? fechaFin = null)
    {
        var rangoNormalizado = (range ?? "todos").Trim();
        var (fechaInicioUsada, fechaFinUsada) = NormalizarRangoFechas(rangoNormalizado, fechaInicio, fechaFin);
        var empresaIdNormalizada = empresaId is > 0 ? empresaId : null;
        var ventas = ObtenerVentasPorRango(configuration, rangoNormalizado, empresaIdNormalizada, nombreProducto, sku, categoria, fechaInicioUsada, fechaFinUsada);

        var resumen = new DashboardSummary
        {
            TotalProductos = ObtenerTotalProductos(configuration, empresaIdNormalizada),
            ProductosPocoStock = ObtenerProductosConStockBajo(configuration, empresaIdNormalizada),
            ProductosSinStock = ObtenerProductosSinStock(configuration, empresaIdNormalizada),
            GananciasPorMesNetas = ventas.Sum(v => v.Precio - v.Costo),
            GananciaPromedioPorProducto = ventas.Any() ? ventas.Average(v => v.Precio - v.Costo) : 0m,
            VentasTotales = ventas.Count,
            VentaPromedioPorMes = ventas.Any() ? ventas.Average(v => v.Precio) : 0m,
            UltimosProductosVendidos = ventas.OrderByDescending(v => v.Fecha).Take(5).ToList(),
            BasesDeDatos = new List<string> { "Productos", "Ventas", "Proveedores", "Dashboard" },
            TotalVentasMes = ventas.Sum(v => v.Precio),
            GananciaNetaMes = ventas.Sum(v => v.Precio - v.Costo),
            GastosTotalesMes = ventas.Sum(v => v.Costo),
            VentaPromedioMes = ventas.Any() ? ventas.Average(v => v.Precio) : 0m,
            MesActualLabel = fechaInicioUsada.ToString("MMMM yyyy"),
            VentasPorPeriodo = ObtenerVentasPorPeriodo(configuration, empresaIdNormalizada, fechaInicioUsada, rangoNormalizado, nombreProducto, sku, categoria, fechaInicioUsada, fechaFinUsada),
            GananciaPorCategoria = ObtenerGananciaPorCategoria(configuration, empresaIdNormalizada, fechaInicioUsada, rangoNormalizado, nombreProducto, sku, categoria, fechaInicioUsada, fechaFinUsada),
            ParticipacionVentas = ObtenerParticipacionVentas(configuration, empresaIdNormalizada, fechaInicioUsada, rangoNormalizado, nombreProducto, sku, categoria, fechaInicioUsada, fechaFinUsada)
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

        resumen.UltimosProductosVendidos = ventas
            .OrderByDescending(v => v.Fecha)
            .ToList();

        return resumen;
    }

    private static DateTime ObtenerFechaInicioPorRango(string range)
    {
        var rangoNormalizado = (range ?? "todos").Trim();
        var ahora = DateTime.Now;

        return rangoNormalizado.ToLowerInvariant() switch
        {
            "dia" => ahora.Date,
            "semana" => ahora.Date.AddDays(-((int)ahora.DayOfWeek == 0 ? 6 : (int)ahora.DayOfWeek - 1)),
            "mes" => new DateTime(ahora.Year, ahora.Month, 1),
            "anio" or "año" => new DateTime(ahora.Year, 1, 1),
            "todos" => ahora.AddYears(-10),
            _ => ahora.AddYears(-10)
        };
    }

    private static (DateTime Inicio, DateTime Fin) NormalizarRangoFechas(string range, DateTime? fechaInicio, DateTime? fechaFin)
    {
        var finDiaActual = DateTime.Today.AddDays(1).AddTicks(-1);
        var inicio = (fechaInicio ?? ObtenerFechaInicioPorRango(range)).Date;
        var finBase = fechaFin ?? finDiaActual;

        if (finBase > finDiaActual)
        {
            finBase = finDiaActual;
        }

        var fin = finBase.TimeOfDay == TimeSpan.Zero
            ? finBase.Date.AddDays(1).AddTicks(-1)
            : finBase;

        if (fin > finDiaActual)
        {
            fin = finDiaActual;
        }

        if (fin < inicio)
        {
            var intercambio = inicio;
            inicio = fin.Date;
            fin = intercambio;

            if (fin.TimeOfDay == TimeSpan.Zero)
            {
                fin = fin.Date.AddDays(1).AddTicks(-1);
            }

            if (fin > finDiaActual)
            {
                fin = finDiaActual;
            }
        }

        return (inicio, fin);
    }

    private static List<VentaRegistro> ObtenerVentasPorRango(IConfiguration configuration, string range, int? empresaId = null, string? nombreProducto = null, string? sku = null, string? categoria = null, DateTime? fechaInicio = null, DateTime? fechaFin = null)
    {
        var rangoNormalizado = (range ?? "todos").Trim();
        var (fechaInicioUsada, fechaFinUsada) = NormalizarRangoFechas(rangoNormalizado, fechaInicio, fechaFin);
        var nombreProductoNormalizado = string.IsNullOrWhiteSpace(nombreProducto) ? null : nombreProducto.Trim();
        var skuNormalizado = string.IsNullOrWhiteSpace(sku) ? null : sku.Trim();
        var categoriaNormalizada = string.IsNullOrWhiteSpace(categoria) ? null : categoria.Trim();

        var fechaFinExclusiva = fechaFinUsada.AddTicks(1);

        using var connection = CrearConexion(configuration);
        const string sql = @"
SELECT
    v.id AS Id,
    d.productoID AS ProductoId,
    p.nombre AS NombreProducto,
    p.sku AS Sku,
    c.nombre AS Categoria,
    v.fecha AS Fecha,
    v.metodoPago AS MetodoPago,
    d.cantidad AS Cantidad,
    d.precioVenta AS Precio,
    d.costoCompra AS Costo
FROM dbo.Ventas v
INNER JOIN dbo.DetalleVenta d ON d.ventaID = v.id
INNER JOIN dbo.Productos p ON p.id = d.productoID
LEFT JOIN dbo.Categoria c ON c.id = p.categoriaID
WHERE v.fecha >= @FechaInicio
  AND v.fecha <  @FechaFinExclusiva
  AND (@EmpresaId IS NULL OR p.empresaID = @EmpresaId)
  AND (@NombreProducto IS NULL OR p.nombre LIKE @NombreProductoLike)
  AND (@Sku IS NULL OR p.sku = @Sku)
  AND (@Categoria IS NULL OR c.nombre = @Categoria)
ORDER BY v.fecha DESC
OPTION (RECOMPILE);";

        return connection.Query<VentaRegistro>(sql, new
        {
            EmpresaId = empresaId,
            FechaInicio = fechaInicioUsada,
            FechaFinExclusiva = fechaFinExclusiva,
            NombreProducto = nombreProductoNormalizado,
            NombreProductoLike = nombreProductoNormalizado == null ? null : $"%{nombreProductoNormalizado}%",
            Sku = skuNormalizado,
            Categoria = categoriaNormalizada
        }, commandTimeout: 120).ToList();
    }

    private static List<ChartPoint> ObtenerVentasPorPeriodo(IConfiguration configuration, int? empresaId, DateTime fecha, string range, string? nombreProducto = null, string? sku = null, string? categoria = null, DateTime? fechaInicio = null, DateTime? fechaFin = null)
    {
        (DateTime Inicio, DateTime Fin) rango = NormalizarRangoFechas(range, fechaInicio ?? fecha, fechaFin);
        using SqlConnection connection = (SqlConnection)CrearConexion(configuration);

        List<ChartPoint> resultado = new List<ChartPoint>();
        DateTime mesActual = new DateTime(rango.Inicio.Year, rango.Inicio.Month, 1);
        DateTime mesFinal = new DateTime(rango.Fin.Year, rango.Fin.Month, 1);

        while (mesActual <= mesFinal)
        {
            DateTime desde = mesActual;
            DateTime hasta = mesActual.AddMonths(1).AddTicks(-1);
            decimal total = connection.QuerySingleOrDefault<decimal?>(@"
                SELECT COALESCE(SUM(d.precioVenta), 0)
                FROM DetalleVenta d
                INNER JOIN Ventas v ON v.id = d.ventaID
                INNER JOIN Productos p ON p.id = d.productoID
                LEFT JOIN Categoria c ON c.id = p.categoriaID
                WHERE (@EmpresaId IS NULL OR p.empresaID = @EmpresaId)
                  AND v.fecha >= @Desde AND v.fecha <= @Hasta
                  AND (@NombreProducto IS NULL OR p.nombre LIKE @NombreProductoLike)
                  AND (@Sku IS NULL OR p.sku = @Sku)
                  AND (@Categoria IS NULL OR c.nombre = @Categoria)",
                new
                {
                    EmpresaId = empresaId,
                    Desde = desde,
                    Hasta = hasta,
                    NombreProducto = nombreProducto,
                    NombreProductoLike = string.IsNullOrWhiteSpace(nombreProducto) ? null : $"%{nombreProducto.Trim()}%",
                    Sku = string.IsNullOrWhiteSpace(sku) ? null : sku.Trim(),
                    Categoria = string.IsNullOrWhiteSpace(categoria) ? null : categoria.Trim()
                }) ?? 0m;

            resultado.Add(new ChartPoint
            {
                Label = desde.ToString("MMM", new CultureInfo("es-ES")),
                Valor = total
            });

            mesActual = mesActual.AddMonths(1);
        }

        return resultado;
    }

    private static List<ChartPoint> ObtenerGananciaPorCategoria(IConfiguration configuration, int? empresaId, DateTime fecha, string range, string? nombreProducto = null, string? sku = null, string? categoria = null, DateTime? fechaInicio = null, DateTime? fechaFin = null)
    {
        (DateTime Inicio, DateTime Fin) rango = NormalizarRangoFechas(range, fechaInicio ?? fecha, fechaFin);
        using SqlConnection connection = (SqlConnection)CrearConexion(configuration);

        List<(string Nombre, decimal Valor)> filas = connection.Query<(string Nombre, decimal Valor)>(@"
            SELECT c.nombre AS Nombre, COALESCE(SUM(d.ganancia), 0) AS Valor
            FROM DetalleVenta d
            INNER JOIN Ventas v ON v.id = d.ventaID
            INNER JOIN Productos p ON p.id = d.productoID
            LEFT JOIN Categoria c ON c.id = p.categoriaID
            WHERE (@EmpresaId IS NULL OR p.empresaID = @EmpresaId)
              AND v.fecha >= @Inicio AND v.fecha <= @Fin
              AND (@NombreProducto IS NULL OR p.nombre LIKE @NombreProductoLike)
              AND (@Sku IS NULL OR p.sku = @Sku)
              AND (@Categoria IS NULL OR c.nombre = @Categoria)
            GROUP BY c.nombre
            ORDER BY Valor DESC",
            new
            {
                EmpresaId = empresaId,
                Inicio = rango.Inicio,
                Fin = rango.Fin,
                NombreProducto = nombreProducto,
                NombreProductoLike = string.IsNullOrWhiteSpace(nombreProducto) ? null : $"%{nombreProducto.Trim()}%",
                Sku = string.IsNullOrWhiteSpace(sku) ? null : sku.Trim(),
                Categoria = string.IsNullOrWhiteSpace(categoria) ? null : categoria.Trim()
            }).ToList();

        return filas.Select(fila => new ChartPoint
        {
            Label = fila.Nombre,
            Valor = fila.Valor
        }).ToList();
    }

    private static List<DashboardCategoryShare> ObtenerParticipacionVentas(IConfiguration configuration, int? empresaId, DateTime fecha, string range, string? nombreProducto = null, string? sku = null, string? categoria = null, DateTime? fechaInicio = null, DateTime? fechaFin = null)
    {
        (DateTime Inicio, DateTime Fin) rango = NormalizarRangoFechas(range, fechaInicio ?? fecha, fechaFin);
        using SqlConnection connection = (SqlConnection)CrearConexion(configuration);

        List<(string Nombre, decimal Valor)> filas = connection.Query<(string Nombre, decimal Valor)>(@"
            SELECT c.nombre AS Nombre, COALESCE(SUM(d.precioVenta), 0) AS Valor
            FROM DetalleVenta d
            INNER JOIN Ventas v ON v.id = d.ventaID
            INNER JOIN Productos p ON p.id = d.productoID
            LEFT JOIN Categoria c ON c.id = p.categoriaID
            WHERE (@EmpresaId IS NULL OR p.empresaID = @EmpresaId)
              AND v.fecha >= @Inicio AND v.fecha <= @Fin
              AND (@NombreProducto IS NULL OR p.nombre LIKE @NombreProductoLike)
              AND (@Sku IS NULL OR p.sku = @Sku)
              AND (@Categoria IS NULL OR c.nombre = @Categoria)
            GROUP BY c.nombre
            ORDER BY Valor DESC",
            new
            {
                EmpresaId = empresaId,
                Inicio = rango.Inicio,
                Fin = rango.Fin,
                NombreProducto = nombreProducto,
                NombreProductoLike = string.IsNullOrWhiteSpace(nombreProducto) ? null : $"%{nombreProducto.Trim()}%",
                Sku = string.IsNullOrWhiteSpace(sku) ? null : sku.Trim(),
                Categoria = string.IsNullOrWhiteSpace(categoria) ? null : categoria.Trim()
            }).ToList();

        decimal total = filas.Sum(fila => fila.Valor);
        return filas.Select(fila => new DashboardCategoryShare
        {
            Nombre = fila.Nombre,
            Valor = total > 0 ? Math.Round((fila.Valor / total) * 100m, 0, MidpointRounding.AwayFromZero) : 0m
        }).ToList();
    }

    public static bool IniciarSesion(string mail, string contrasena, HttpContext httpContext, IConfiguration configuration)
    {
        string correo = NormalizarMail(mail);
        string clave = contrasena ?? string.Empty;

        if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(clave))
        {
            return false;
        }

        using SqlConnection connection = (SqlConnection)CrearConexion(configuration);
        Usuario? usuario = connection.QuerySingleOrDefault<Usuario>(@"
            SELECT TOP 1 u.id AS Id,
                         u.nombreUsuario AS NombreUsuario,
                         u.empresaID AS EmpresaID,
                         u.rolID AS RolID,
                         u.mail AS Mail,
                         u.numeroTelefono AS NumeroTelefono
            FROM Usuarios u
            WHERE LOWER(LTRIM(RTRIM(u.mail))) = LOWER(LTRIM(RTRIM(@Mail)))
              AND u.contrasena = @Contrasena",
            new { Mail = correo, Contrasena = clave });

        if (usuario == null)
        {
            return false;
        }

        Empresa? empresa = connection.QuerySingleOrDefault<Empresa>(
            "SELECT * FROM Empresa WHERE id = @EmpresaId",
            new { EmpresaId = usuario.EmpresaID });

        httpContext.Session.SetInt32("UsuarioID", usuario.Id);
        httpContext.Session.SetInt32("EmpresaID", usuario.EmpresaID);
        httpContext.Session.SetInt32("RolID", usuario.RolID);
        httpContext.Session.SetString("NombreUsuario", usuario.NombreUsuario);
        httpContext.Session.SetString("NombreEmpresa", empresa?.Nombre ?? $"Empresa de {usuario.NombreUsuario}");

        return true;
    }

    public static bool Registrarse(Usuario usuario, IConfiguration configuration)
    {
        if (usuario == null)
        {
            return false;
        }

        string mail = NormalizarMail(usuario.Mail);
        string nombreUsuario = (usuario.NombreUsuario ?? string.Empty).Trim();
        string nombreEmpresa = (usuario.NombreEmpresa ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(mail)
            || string.IsNullOrWhiteSpace(nombreUsuario)
            || string.IsNullOrWhiteSpace(nombreEmpresa)
            || string.IsNullOrWhiteSpace(usuario.Contrasena))
        {
            return false;
        }

        using SqlConnection connection = (SqlConnection)CrearConexion(configuration);

        int? empresaId = connection.QuerySingleOrDefault<int?>(@"
            SELECT TOP 1 id FROM Empresa
            WHERE LOWER(LTRIM(RTRIM(nombre))) = LOWER(LTRIM(RTRIM(@Nombre)))",
            new { Nombre = nombreEmpresa });

        if (!empresaId.HasValue)
        {
            empresaId = connection.QuerySingle<int>(@"
                INSERT INTO Empresa (nombre) VALUES (@Nombre);
                SELECT CAST(SCOPE_IDENTITY() AS int)",
                new { Nombre = nombreEmpresa });
        }

        int existeMail = connection.ExecuteScalar<int>(@"
            SELECT COUNT(1) FROM Usuarios
            WHERE LOWER(LTRIM(RTRIM(mail))) = LOWER(LTRIM(RTRIM(@Mail)))",
            new { Mail = mail });

        if (existeMail > 0)
        {
            return false;
        }

        int? rolId = connection.QuerySingleOrDefault<int?>(@"
            SELECT TOP 1 id FROM Rol
            WHERE empresaID = @EmpresaId
            ORDER BY id",
            new { EmpresaId = empresaId.Value });

        if (!rolId.HasValue)
        {
            int permisosId = connection.QuerySingle<int>(@"
                INSERT INTO Permisos
                    (registrarVentas, verHistorialVentas, anularVentas, verProductos,
                     crearProductos, editarProductos, eliminarProductos, verStock,
                     verDashboard, gestionarUsuarios, verGanacia)
                VALUES (1,1,1,1,1,1,1,1,1,1,1);
                SELECT CAST(SCOPE_IDENTITY() AS int)");

            rolId = connection.QuerySingle<int>(@"
                INSERT INTO Rol (empresaID, nombre, permisoID)
                VALUES (@EmpresaId, @Nombre, @PermisoId);
                SELECT CAST(SCOPE_IDENTITY() AS int)",
                new { EmpresaId = empresaId.Value, Nombre = "Administrador", PermisoId = permisosId });
        }

        connection.Execute(@"
            INSERT INTO Usuarios (nombreUsuario, contrasena, mail, numeroTelefono, empresaID, rolID)
            VALUES (@NombreUsuario, @Contrasena, @Mail, @Telefono, @EmpresaId, @RolId)",
            new
            {
                NombreUsuario = nombreUsuario,
                Contrasena = usuario.Contrasena,
                Mail = mail,
                Telefono = usuario.NumeroTelefono,
                EmpresaId = empresaId.Value,
                RolId = rolId.Value
            });

        return true;
    }

    public static int? ObtenerUsuarioLogueadoID(HttpContext httpContext)
    {
        return httpContext.Session.GetInt32("UsuarioID");
    }

    public static int ObtenerEmpresaIdActual(IConfiguration configuration, HttpContext httpContext)
    {
        int? empresaId = httpContext.Session.GetInt32("EmpresaID");
        if (empresaId.HasValue && empresaId.Value > 0)
        {
            return empresaId.Value;
        }

        int? usuarioId = httpContext.Session.GetInt32("UsuarioID");
        if (!usuarioId.HasValue || usuarioId.Value <= 0)
        {
            return 0;
        }

        using SqlConnection connection = (SqlConnection)CrearConexion(configuration);
        int? empresa = connection.QuerySingleOrDefault<int?>(
            "SELECT empresaID FROM Usuarios WHERE id = @UsuarioId",
            new { UsuarioId = usuarioId.Value });

        if (empresa.HasValue && empresa.Value > 0)
        {
            httpContext.Session.SetInt32("EmpresaID", empresa.Value);
        }

        return empresa ?? 0;
    }

    public static void CerrarSesion(HttpContext httpContext)
    {
        httpContext.Session.Clear();
    }

    public static async Task<PermisosLectura?> ObtenerPermisosLecturaAsync(IConfiguration configuration, int usuarioId, int empresaId)
    {
        using SqlConnection connection = (SqlConnection)CrearConexion(configuration);
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
            WHERE u.id = @UsuarioId AND u.empresaID = @EmpresaId",
            new { UsuarioId = usuarioId, EmpresaId = empresaId });
    }

    public static async Task<ResumenStock> ObtenerResumenStockAsync(IConfiguration configuration, int empresaId)
    {
        using SqlConnection connection = (SqlConnection)CrearConexion(configuration);
        return await connection.QuerySingleOrDefaultAsync<ResumenStock>(@"
            SELECT COUNT(*) AS TotalProductos,
                   SUM(CASE WHEN ISNULL(s.cantidad, 0) <= 0 THEN 1 ELSE 0 END) AS Agotados,
                   SUM(CASE WHEN ISNULL(s.cantidad, 0) > 0 AND ISNULL(s.cantidad, 0) <= ISNULL(s.cantidadMinimoAlerta, 0) THEN 1 ELSE 0 END) AS BajoMinimo
            FROM Productos p
            LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
            WHERE p.empresaID = @EmpresaId",
            new { EmpresaId = empresaId }) ?? new ResumenStock();
    }

    public static async Task<List<ProductoLectura>> ObtenerProductosLecturaAsync(IConfiguration configuration, int empresaId, bool incluirStockBajo = true)
    {
        using SqlConnection connection = (SqlConnection)CrearConexion(configuration);
        string sql = @"
            SELECT TOP (80) p.nombre AS Nombre, p.sku AS Sku, c.nombre AS Categoria,
                   ISNULL(s.cantidad, 0) AS StockActual,
                   ISNULL(s.cantidadMinimoAlerta, 0) AS StockMinimo,
                   p.precioVenta AS Precio, p.costoCompra AS CostoCompra
            FROM Productos p
            LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
            LEFT JOIN Categoria c ON c.id = p.categoriaID
            WHERE p.empresaID = @EmpresaId
            ORDER BY CASE WHEN @IncluirStockBajo = 1 AND (ISNULL(s.cantidad, 0) <= ISNULL(s.cantidadMinimoAlerta, 0) OR ISNULL(s.cantidad, 0) = 0) THEN 0 ELSE 1 END,
                     ISNULL(s.cantidad, 0) ASC, p.nombre ASC";
        return (await connection.QueryAsync<ProductoLectura>(sql,
            new { EmpresaId = empresaId, IncluirStockBajo = incluirStockBajo })).ToList();
    }

    public static async Task<List<AlertaStockRegistro>> ObtenerAlertasStockAsync(IConfiguration configuration, int empresaId)
    {
        using SqlConnection connection = (SqlConnection)CrearConexion(configuration);
        return (await connection.QueryAsync<AlertaStockRegistro>(@"
            SELECT p.id AS Id, p.nombre AS Nombre, p.sku AS Sku,
                   ISNULL(s.cantidad, 0) AS Stock,
                   ISNULL(s.cantidadMinimoAlerta, 0) AS Minimo
            FROM Productos p
            LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
            WHERE p.empresaID = @EmpresaId
            ORDER BY ISNULL(s.cantidad, 0) ASC, p.nombre ASC",
            new { EmpresaId = empresaId })).ToList();
    }

    public static int GuardarReporte(IConfiguration configuration, ReporteGuardado reporte)
    {
        if (reporte.UsuarioID <= 0 || reporte.EmpresaID <= 0 || string.IsNullOrWhiteSpace(reporte.Nombre))
        {
            return 0;
        }

        using SqlConnection connection = (SqlConnection)CrearConexion(configuration);
        string sql = @"
            INSERT INTO Reportes
            (usuarioID, empresaID, nombre, tipoReporte, periodoTiempo, formatoReporte,
             frecuenciaActualizacion, fechaCreacion, vistaPreviaHtml, totalVentas, ganancia,
             gastos, ventaPromedio, cantidadProductosVendidos, totalProductos,
             productosPocoStock, productosSinStock, margenNeto, productosStockSobra,
             hallazgos, recomendaciones)
            VALUES
            (@UsuarioID, @EmpresaID, @Nombre, @TipoReporte, @PeriodoTiempo, @FormatoReporte,
             @FrecuenciaActualizacion, @FechaCreacion, @VistaPreviaHtml, @TotalVentas, @Ganancia,
             @Gastos, @VentaPromedio, @CantidadProductosVendidos, @TotalProductos,
             @ProductosPocoStock, @ProductosSinStock, @MargenNeto, @StockSobra,
             @Hallazgos, @Recomendaciones);
            SELECT CAST(SCOPE_IDENTITY() AS int);";

        return connection.QuerySingle<int>(sql, reporte);
    }

    public static List<ReporteGuardado> ObtenerReportes(IConfiguration configuration, int usuarioId, int empresaId)
    {
        if (usuarioId <= 0 || empresaId <= 0)
        {
            return new List<ReporteGuardado>();
        }

        using SqlConnection connection = (SqlConnection)CrearConexion(configuration);
        string sql = @"
            SELECT id AS Id, usuarioID AS UsuarioID, empresaID AS EmpresaID,
                   nombre AS Nombre, tipoReporte AS TipoReporte, periodoTiempo AS PeriodoTiempo,
                   formatoReporte AS FormatoReporte, frecuenciaActualizacion AS FrecuenciaActualizacion,
                   fechaCreacion AS FechaCreacion, vistaPreviaHtml AS VistaPreviaHtml,
                   totalVentas AS TotalVentas, ganancia AS Ganancia, gastos AS Gastos,
                   ventaPromedio AS VentaPromedio, cantidadProductosVendidos AS CantidadProductosVendidos,
                   totalProductos AS TotalProductos, productosPocoStock AS ProductosPocoStock,
                   productosSinStock AS ProductosSinStock, margenNeto AS MargenNeto,
                   productosStockSobra AS StockSobra,
                   COALESCE(hallazgos, '') AS Hallazgos,
                   COALESCE(recomendaciones, '') AS Recomendaciones
            FROM Reportes
            WHERE usuarioID = @UsuarioId AND empresaID = @EmpresaId
            ORDER BY fechaCreacion DESC, id DESC";

        return connection.Query<ReporteGuardado>(sql,
            new { UsuarioId = usuarioId, EmpresaId = empresaId }).ToList();
    }

    public static ReporteGuardado? ObtenerReporte(IConfiguration configuration, int reporteId, int usuarioId, int empresaId)
    {
        if (reporteId <= 0 || usuarioId <= 0 || empresaId <= 0)
        {
            return null;
        }

        using SqlConnection connection = (SqlConnection)CrearConexion(configuration);
        string sql = @"
            SELECT id AS Id, usuarioID AS UsuarioID, empresaID AS EmpresaID,
                   nombre AS Nombre, tipoReporte AS TipoReporte, periodoTiempo AS PeriodoTiempo,
                   formatoReporte AS FormatoReporte, frecuenciaActualizacion AS FrecuenciaActualizacion,
                   fechaCreacion AS FechaCreacion, vistaPreviaHtml AS VistaPreviaHtml,
                   totalVentas AS TotalVentas, ganancia AS Ganancia, gastos AS Gastos,
                   ventaPromedio AS VentaPromedio, cantidadProductosVendidos AS CantidadProductosVendidos,
                   totalProductos AS TotalProductos, productosPocoStock AS ProductosPocoStock,
                   productosSinStock AS ProductosSinStock, margenNeto AS MargenNeto,
                   productosStockSobra AS StockSobra,
                   COALESCE(hallazgos, '') AS Hallazgos,
                   COALESCE(recomendaciones, '') AS Recomendaciones
            FROM Reportes
            WHERE id = @ReporteId AND usuarioID = @UsuarioId AND empresaID = @EmpresaId";

        return connection.QueryFirstOrDefault<ReporteGuardado>(sql,
            new { ReporteId = reporteId, UsuarioId = usuarioId, EmpresaId = empresaId });
    }

    public static void GarantizarEsquemaActual(IConfiguration configuration)
    {
        using SqlConnection connection = (SqlConnection)CrearConexion(configuration);
        connection.Open();

        connection.Execute(@"
IF OBJECT_ID(N'dbo.Ventas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ventas (
        id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        empresaID int NOT NULL,
        usuarioID int NOT NULL,
        fecha datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
        metodoPago varchar(50) NOT NULL,
        totalVenta decimal(12,2) NOT NULL DEFAULT 0,
        observacion varchar(500) NULL
    );
END;

IF OBJECT_ID(N'dbo.DetalleVenta', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DetalleVenta (
        id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ventaID int NOT NULL,
        productoID int NOT NULL,
        cantidad int NOT NULL,
        precioVenta decimal(12,2) NOT NULL,
        costoCompra decimal(12,2) NOT NULL DEFAULT 0,
        porcentajeGanancia decimal(10,2) NULL,
        ganancia decimal(12,2) NOT NULL DEFAULT 0
    );
END;

IF OBJECT_ID(N'dbo.IAChats', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IAChats (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        UsuarioId int NOT NULL,
        EmpresaId int NOT NULL,
        Titulo nvarchar(120) NOT NULL,
        CreadoEn datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ActualizadoEn datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;

IF OBJECT_ID(N'dbo.IAMensajes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IAMensajes (
        Id bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ChatId uniqueidentifier NOT NULL,
        Rol varchar(16) NOT NULL,
        Texto nvarchar(4000) NOT NULL,
        ContenidoJson nvarchar(max) NULL,
        CreadoEn datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
ELSE
BEGIN
    IF COL_LENGTH(N'dbo.IAMensajes', N'ContenidoJson') IS NULL
        ALTER TABLE dbo.IAMensajes ADD ContenidoJson nvarchar(max) NULL;
END;

IF OBJECT_ID(N'dbo.IAAuditoria', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IAAuditoria (
        Id bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UsuarioId int NOT NULL,
        EmpresaId int NOT NULL,
        Accion varchar(30) NOT NULL,
        ProductoId int NULL,
        Antes nvarchar(1000) NULL,
        Despues nvarchar(1000) NULL,
        Fecha datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;");
    }

    public static async Task<PerfilAjustesDatos?> ObtenerDatosAjustesAsync(IDbConnection connection, int usuarioId, int empresaId)
    {
        var datos = await connection.QuerySingleOrDefaultAsync<PerfilAjustesDatos>(@"
            SELECT u.nombreUsuario AS NombreUsuario,
                   u.mail AS Mail,
                   u.numeroTelefono AS NumeroTelefono,
                   e.nombre AS NombreEmpresa,
                   CAST(CASE WHEN r.permisoID IS NOT NULL THEN 1 ELSE 0 END AS bit) AS PuedeEditarEmpresa
            FROM Usuarios u
            INNER JOIN Empresa e ON e.id = u.empresaID
            INNER JOIN Rol r ON r.id = u.rolID
            WHERE u.id = @UsuarioId AND u.empresaID = @EmpresaId",
            new { UsuarioId = usuarioId, EmpresaId = empresaId });
        return datos;
    }

    public static async Task<DatosRepetidos> ObtenerDatosRepetidosAsync(IDbConnection connection, string mail, string nombre, int usuarioId)
    {
        var datos = await connection.QuerySingleAsync<DatosRepetidos>(@"
            SELECT SUM(CASE WHEN LOWER(LTRIM(RTRIM(mail))) = LOWER(LTRIM(RTRIM(@Mail))) AND id <> @UsuarioId THEN 1 ELSE 0 END) AS Mail,
                   SUM(CASE WHEN LOWER(LTRIM(RTRIM(nombreUsuario))) = LOWER(LTRIM(RTRIM(@Nombre))) AND id <> @UsuarioId THEN 1 ELSE 0 END) AS Nombre
            FROM Usuarios",
            new { Mail = mail, Nombre = nombre, UsuarioId = usuarioId });
        return datos;
    }

    public static async Task GuardarPerfilAsync(IDbConnection connection, string nombre, string mail, string? telefono, int usuarioId, int empresaId)
    {
        await connection.ExecuteAsync(@"
            UPDATE Usuarios
            SET nombreUsuario = @Nombre,
                mail = @Mail,
                numeroTelefono = @Telefono
            WHERE id = @UsuarioId AND empresaID = @EmpresaId",
            new { Nombre = nombre, Mail = mail, Telefono = telefono, UsuarioId = usuarioId, EmpresaId = empresaId });
    }

    public static async Task<int> TienePermisoEmpresaAsync(IDbConnection connection, int usuarioId, int empresaId)
    {
        return await connection.ExecuteScalarAsync<int>(@"
            SELECT CAST(CASE WHEN p.gestionarUsuarios = 1 THEN 1 ELSE 0 END AS int)
            FROM Usuarios u
            INNER JOIN Rol r ON r.id = u.rolID
            INNER JOIN Permisos p ON p.id = r.permisoID
            WHERE u.id = @UsuarioId AND u.empresaID = @EmpresaId",
            new { UsuarioId = usuarioId, EmpresaId = empresaId });
    }

    public static async Task GuardarEmpresaAsync(IDbConnection connection, string nombre, int empresaId)
    {
        await connection.ExecuteAsync("UPDATE Empresa SET nombre = @Nombre WHERE id = @EmpresaId",
            new { Nombre = nombre, EmpresaId = empresaId });
    }

    public static async Task<string?> ObtenerContrasenaActualAsync(IDbConnection connection, int usuarioId, int empresaId)
    {
        return await connection.ExecuteScalarAsync<string?>(@"
            SELECT contrasena FROM Usuarios
            WHERE id = @UsuarioId AND empresaID = @EmpresaId",
            new { UsuarioId = usuarioId, EmpresaId = empresaId });
    }

    public static async Task CambiarContrasenaAsync(IDbConnection connection, string contrasenaNueva, int usuarioId, int empresaId)
    {
        await connection.ExecuteAsync(@"
            UPDATE Usuarios SET contrasena = @Contrasena
            WHERE id = @UsuarioId AND empresaID = @EmpresaId",
            new { Contrasena = contrasenaNueva, UsuarioId = usuarioId, EmpresaId = empresaId });
    }

    public static async Task<int> RegistroVentaAsync(IDbConnection connection, int productoId, string nombreProducto, string metodoPago,
        int cantidad, decimal precioVentaTotal, decimal precioCompraTotal, int usuarioId, int empresaId, IDbTransaction transaction)
    {
        _ = productoId;
        _ = nombreProducto;
        _ = cantidad;
        return await connection.QuerySingleAsync<int>(@"
            INSERT INTO Ventas (empresaID, usuarioID, fecha, metodoPago, totalVenta, observacion)
            VALUES (@EmpresaId, @UsuarioId, @Fecha, @MetodoPago, @TotalVenta, NULL);
            SELECT CAST(SCOPE_IDENTITY() AS int);",
            new
            {
                EmpresaId = empresaId,
                UsuarioId = usuarioId,
                Fecha = DateTime.Now,
                MetodoPago = metodoPago,
                TotalVenta = precioVentaTotal
            }, transaction);
    }

    public static async Task<int> InsertarDetalleVentaAsync(IDbConnection connection, int ventaId, int productoId, int cantidad,
        decimal precioVentaTotal, decimal precioCompraTotal, decimal porcentajeGanancia, decimal ganancia, IDbTransaction transaction)
    {
        return await connection.ExecuteAsync(@"
            INSERT INTO DetalleVenta
            (ventaID, productoID, cantidad, precioVenta, costoCompra, porcentajeGanancia, ganancia)
            VALUES
            (@VentaId, @ProductoId, @Cantidad, @PrecioVenta, @CostoCompra, @PorcentajeGanancia, @Ganancia);",
            new
            {
                VentaId = ventaId,
                ProductoId = productoId,
                Cantidad = cantidad,
                PrecioVenta = precioVentaTotal,
                CostoCompra = precioCompraTotal,
                PorcentajeGanancia = porcentajeGanancia,
                Ganancia = ganancia
            }, transaction);
    }

    public static async Task<bool> DescontarStockAsync(IDbConnection connection, int productoId, int cantidad, IDbTransaction transaction)
    {
        var filas = await connection.ExecuteAsync(@"
            UPDATE Stock
            SET cantidad = cantidad - @Cantidad,
                actualizadoEn = @ActualizadoEn
            WHERE productoID = @ProductoId AND cantidad >= @Cantidad",
            new { ProductoId = productoId, Cantidad = cantidad, ActualizadoEn = DateTime.Now }, transaction);
        return filas == 1;
    }

    public static async Task<AnalisisVentasGananciasData> ObtenerAnalisisVentasGananciasAsync(IDbConnection connection, int empresaId, DateTime inicio, DateTime fin)
    {
        if (connection.State != ConnectionState.Open)
            connection.Open();

        var filas = (await connection.QueryAsync<VentaRegistro>(@"
            SELECT v.fecha AS Fecha, d.cantidad AS Cantidad,
                   d.precioVenta AS Precio, d.costoCompra AS Costo,
                   p.nombre AS NombreProducto, c.nombre AS Categoria
            FROM Ventas v
            INNER JOIN DetalleVenta d ON d.ventaID = v.id
            INNER JOIN Productos p ON p.id = d.productoID
            LEFT JOIN Categoria c ON c.id = p.categoriaID
            WHERE v.empresaID = @EmpresaId AND v.fecha >= @Inicio AND v.fecha <= @Fin",
            new { EmpresaId = empresaId, Inicio = inicio, Fin = fin })).ToList();

        var ventasTotales = filas.Sum(v => v.Precio);
        var gastosTotales = filas.Sum(v => v.Costo);
        var ganancias = ventasTotales - gastosTotales;

        return new AnalisisVentasGananciasData
        {
            VentasTotales = ventasTotales,
            GastosTotales = gastosTotales,
            VentaPromedio = filas.Count == 0 ? 0 : filas.Average(v => v.Precio),
            Ganancias = ganancias,
            VentasTotalesCantidad = filas.Sum(v => v.Cantidad),
            VentasDiarias = filas.GroupBy(v => v.Fecha.Date).Select(g => new ChartPoint { Label = g.Key.ToString("dd/MM"), Valor = g.Sum(v => v.Precio) }).ToList(),
            VentaPorSemana = filas.GroupBy(v => v.Fecha.DayOfWeek).Select(g => new ChartPoint { Label = g.Key.ToString(), Valor = g.Sum(v => v.Precio) }).ToList(),
            GananciaPorCategoria = filas.GroupBy(v => v.Categoria).Select(g => new ChartPoint { Label = g.Key, Valor = g.Sum(v => v.Precio - v.Costo) }).ToList(),
            MargenNeto = ventasTotales == 0 ? 0 : Math.Round(ganancias / ventasTotales * 100m, 2),
            FormulaGanancia = ganancias
        };
    }

    public static async Task<AnalisisProductoData> ObtenerAnalisisProductoAsync(IDbConnection connection, int empresaId, DateTime inicio, DateTime fin)
    {
        if (connection.State != ConnectionState.Open)
            connection.Open();

        var ventas = (await connection.QueryAsync<AnalisisVentaProductoRow>(@"
            SELECT d.productoID AS ProductoId,
                   SUM(d.cantidad) AS CantidadVendida,
                   SUM(d.precioVenta) AS VentasTotales,
                   SUM(d.costoCompra) AS GastoTotal,
                   SUM(d.ganancia) AS GananciaTotal
            FROM DetalleVenta d
            INNER JOIN Ventas v ON v.id = d.ventaID
            INNER JOIN Productos p ON p.id = d.productoID
            WHERE v.empresaID = @EmpresaId AND v.fecha >= @Inicio AND v.fecha <= @Fin
            GROUP BY d.productoID",
            new { EmpresaId = empresaId, Inicio = inicio, Fin = fin })).ToList();

        var productos = (await connection.QueryAsync<AnalisisProductoRow>(@"
            SELECT p.id AS Id, p.nombre AS Nombre, p.sku AS Sku,
                   c.nombre AS Categoria, p.precioVenta AS Precio,
                   p.costoCompra AS CostoCompra,
                   ISNULL(s.cantidad, 0) AS StockActual,
                   ISNULL(s.cantidadMinimoAlerta, 0) AS StockMinimo
            FROM Productos p
            LEFT JOIN Categoria c ON c.id = p.categoriaID
            LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
            WHERE p.empresaID = @EmpresaId",
            new { EmpresaId = empresaId })).ToList();

        foreach (var producto in productos)
        {
            var venta = ventas.FirstOrDefault(v => v.ProductoId == producto.Id);
            producto.CantidadVendida = venta?.CantidadVendida ?? 0;
        }

        return new AnalisisProductoData
        {
            Productos = productos,
            VentasPorProducto = ventas,
            VentasPorMes = new List<dynamic>(),
            InteresCompraPorMes = new List<dynamic>(),
            VentasSemana = new List<dynamic>()
        };
    }

    public static async Task<bool> ExisteTablaVentasAsync(IConfiguration configuration)
    {
        using var connection = (SqlConnection)CrearConexion(configuration);
        return await connection.ExecuteScalarAsync<int>("SELECT CASE WHEN OBJECT_ID('dbo.Ventas','U') IS NULL THEN 0 ELSE 1 END") == 1;
    }

    public static async Task<List<VentaDiaria>> ObtenerVentasUltimos30DiasAsync(IConfiguration configuration, int empresaId)
    {
        using var connection = (SqlConnection)CrearConexion(configuration);
        return (await connection.QueryAsync<VentaDiaria>(@"
            SELECT CAST(v.fecha AS date) AS Fecha,
                   COUNT(DISTINCT v.id) AS Operaciones,
                   SUM(d.cantidad) AS Unidades,
                   SUM(d.precioVenta) AS Total,
                   SUM(d.costoCompra) AS Costo
            FROM Ventas v
            INNER JOIN DetalleVenta d ON d.ventaID = v.id
            WHERE v.empresaID = @EmpresaId AND v.fecha >= DATEADD(DAY,-30,CAST(GETDATE() AS date))
            GROUP BY CAST(v.fecha AS date)
            ORDER BY Fecha",
            new { EmpresaId = empresaId })).ToList();
    }

    public static async Task<List<ProductoVentaIA>> ObtenerRankingProductosUltimos30DiasAsync(IConfiguration configuration, int empresaId)
    {
        using var connection = (SqlConnection)CrearConexion(configuration);
        return (await connection.QueryAsync<ProductoVentaIA>(@"
            SELECT p.nombre AS Nombre, p.sku AS Sku,
                   SUM(d.cantidad) AS Unidades,
                   SUM(d.precioVenta) AS Facturacion,
                   SUM(d.ganancia) AS Ganancia
            FROM DetalleVenta d
            INNER JOIN Ventas v ON v.id = d.ventaID
            INNER JOIN Productos p ON p.id = d.productoID
            WHERE v.empresaID = @EmpresaId AND v.fecha >= DATEADD(DAY,-30,CAST(GETDATE() AS date))
            GROUP BY p.nombre, p.sku
            ORDER BY Unidades DESC",
            new { EmpresaId = empresaId })).ToList();
    }

    public static async Task<List<ProductoVentaIA>> ObtenerRankingProductosAsync(IConfiguration configuration, int empresaId, int? dias)
    {
        using var connection = (SqlConnection)CrearConexion(configuration);
        var filtroFecha = dias.HasValue ? "AND v.fecha >= DATEADD(DAY, @Dias, CAST(GETDATE() AS date))" : "";
        var sql = @"
            SELECT p.nombre AS Nombre, p.sku AS Sku,
                   SUM(d.cantidad) AS Unidades,
                   SUM(d.precioVenta) AS Facturacion,
                   SUM(d.ganancia) AS Ganancia
            FROM DetalleVenta d
            INNER JOIN Ventas v ON v.id = d.ventaID
            INNER JOIN Productos p ON p.id = d.productoID
            WHERE v.empresaID = @EmpresaId " + filtroFecha + @"
            GROUP BY p.nombre, p.sku
            ORDER BY Unidades DESC";
        return (await connection.QueryAsync<ProductoVentaIA>(sql,
            new { EmpresaId = empresaId, Dias = dias.HasValue ? -dias.Value : 0 })).ToList();
    }

    public static async Task<bool> IAPermitidoAsync(IConfiguration configuration, int usuarioId, int empresaId, string permiso)
    {
        using var connection = (SqlConnection)CrearConexion(configuration);
        var columna = permiso switch
        {
            "crear" => "crearProductos",
            "editar" or "precio" or "stock" => "editarProductos",
            "eliminar" => "eliminarProductos",
            "verStock" => "verStock",
            "verProductos" => "verProductos",
            "verVentas" => "verHistorialVentas",
            _ => null
        };
        if (columna is null) return false;
        var sql = $@"
            SELECT CAST(CASE WHEN p.{columna} = 1 THEN 1 ELSE 0 END AS bit)
            FROM Usuarios u
            INNER JOIN Rol r ON r.id = u.rolID
            INNER JOIN Permisos p ON p.id = r.permisoID
            WHERE u.id = @UsuarioId AND u.empresaID = @EmpresaId";
        return await connection.ExecuteScalarAsync<bool>(sql, new { UsuarioId = usuarioId, EmpresaId = empresaId });
    }

    public static Task<bool> IAUsuarioTienePermisoAsync(IConfiguration configuration, int usuarioId, int empresaId, string permiso)
        => IAPermitidoAsync(configuration, usuarioId, empresaId, permiso);

    public static async Task<bool> IAUsuarioValidoAsync(IConfiguration configuration, int usuarioId, int empresaId)
    {
        using var connection = (SqlConnection)CrearConexion(configuration);
        return await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1) FROM Usuarios
            WHERE id = @UsuarioId AND empresaID = @EmpresaId",
            new { UsuarioId = usuarioId, EmpresaId = empresaId }) == 1;
    }

    public static async Task<List<IAChatResumen>> IAListarChatsAsync(IConfiguration configuration, int usuarioId, int empresaId)
    {
        using var connection = (SqlConnection)CrearConexion(configuration);
        return (await connection.QueryAsync<IAChatResumen>(@"
            SELECT Id, Titulo, ActualizadoEn
            FROM IAChats
            WHERE UsuarioId = @UsuarioId AND EmpresaId = @EmpresaId
            ORDER BY ActualizadoEn DESC",
            new { UsuarioId = usuarioId, EmpresaId = empresaId })).ToList();
    }

    public static async Task<Guid> IACrearChatAsync(IConfiguration configuration, int usuarioId, int empresaId)
    {
        using var connection = (SqlConnection)CrearConexion(configuration);
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(@"
            INSERT INTO IAChats (Id, UsuarioId, EmpresaId, Titulo, CreadoEn, ActualizadoEn)
            VALUES (@Id, @UsuarioId, @EmpresaId, @Titulo, @Ahora, @Ahora)",
            new { Id = id, UsuarioId = usuarioId, EmpresaId = empresaId, Titulo = "Nuevo chat", Ahora = DateTime.UtcNow });
        return id;
    }

    public static async Task<bool> IAExisteChatAsync(IConfiguration configuration, Guid chatId, int usuarioId, int empresaId)
    {
        using var connection = (SqlConnection)CrearConexion(configuration);
        return await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1) FROM IAChats
            WHERE Id = @ChatId AND UsuarioId = @UsuarioId AND EmpresaId = @EmpresaId",
            new { ChatId = chatId, UsuarioId = usuarioId, EmpresaId = empresaId }) == 1;
    }

    public static async Task<List<IAMensaje>> IALeerChatAsync(IConfiguration configuration, Guid chatId, int usuarioId, int empresaId)
    {
        using var connection = (SqlConnection)CrearConexion(configuration);
        var pertenece = await IAExisteChatAsync(configuration, chatId, usuarioId, empresaId);
        if (!pertenece) return new List<IAMensaje>();

        return (await connection.QueryAsync<IAMensaje>(@"
            SELECT Rol,
                   Texto AS Text,
                   ContenidoJson
            FROM IAMensajes
            WHERE ChatId = @ChatId
            ORDER BY Id ASC",
            new { ChatId = chatId })).ToList();
    }

    public static async Task GuardarIntercambioAsync(IConfiguration configuration, Guid chatId, int usuarioId, int empresaId,
        string pregunta, string respuesta, string? titulo, string contenidoJson)
    {
        using var connection = (SqlConnection)CrearConexion(configuration);
        if (!await IAExisteChatAsync(configuration, chatId, usuarioId, empresaId)) return;

        await connection.ExecuteAsync(@"
            INSERT INTO IAMensajes (ChatId, Rol, Texto, ContenidoJson, CreadoEn)
            VALUES (@ChatId, 'user', @Pregunta, NULL, @Ahora);
            INSERT INTO IAMensajes (ChatId, Rol, Texto, ContenidoJson, CreadoEn)
            VALUES (@ChatId, 'assistant', @Respuesta, @ContenidoJson, @Ahora);
            UPDATE IAChats
            SET Titulo = CASE WHEN @Titulo IS NULL OR LTRIM(RTRIM(@Titulo)) = '' THEN Titulo ELSE @Titulo END,
                ActualizadoEn = @Ahora
            WHERE Id = @ChatId AND UsuarioId = @UsuarioId AND EmpresaId = @EmpresaId",
            new
            {
                ChatId = chatId,
                UsuarioId = usuarioId,
                EmpresaId = empresaId,
                Pregunta = pregunta,
                Respuesta = respuesta,
                ContenidoJson = contenidoJson,
                Titulo = titulo,
                Ahora = DateTime.UtcNow
            });
    }

    public static async Task<bool> IARenombrarChatAsync(IConfiguration configuration, Guid chatId, int usuarioId, int empresaId, string titulo)
    {
        using var connection = (SqlConnection)CrearConexion(configuration);
        return await connection.ExecuteAsync(@"
            UPDATE IAChats
            SET Titulo = @Titulo, ActualizadoEn = @Ahora
            WHERE Id = @ChatId AND UsuarioId = @UsuarioId AND EmpresaId = @EmpresaId",
            new { ChatId = chatId, UsuarioId = usuarioId, EmpresaId = empresaId, Titulo = titulo, Ahora = DateTime.UtcNow }) == 1;
    }

    public static async Task<bool> IAEliminarChatAsync(IConfiguration configuration, Guid chatId, int usuarioId, int empresaId)
    {
        using var connection = (SqlConnection)CrearConexion(configuration);
        return await connection.ExecuteAsync(@"
            DELETE FROM IAChats
            WHERE Id = @ChatId AND UsuarioId = @UsuarioId AND EmpresaId = @EmpresaId",
            new { ChatId = chatId, UsuarioId = usuarioId, EmpresaId = empresaId }) == 1;
    }

    public static async Task<List<IAProductoSql.Item>> IABuscarProductoPorSkuAsync(IConfiguration configuration, int empresaId, string sku, SqlTransaction? transaction = null)
    {
        var connection = transaction?.Connection as SqlConnection ?? (SqlConnection)CrearConexion(configuration);
        var cierraConexion = transaction == null;
        try
        {
            var filas = await connection.QueryAsync<IAProductoSql.Item>(@"
                SELECT p.id AS Id, p.nombre AS Nombre, p.sku AS Sku,
                       p.precioVenta AS Precio,
                       ISNULL(s.cantidad, 0) AS Stock
                FROM Productos p
                LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
                WHERE p.empresaID = @EmpresaId
                  AND LOWER(LTRIM(RTRIM(p.sku))) = LOWER(LTRIM(RTRIM(@Sku)))",
                new { EmpresaId = empresaId, Sku = sku }, transaction);
            return filas.ToList();
        }
        finally
        {
            if (cierraConexion) connection.Dispose();
        }
    }

    public static async Task<bool> IAProductoExisteSkuAsync(IConfiguration configuration, int empresaId, string sku)
    {
        using var connection = (SqlConnection)CrearConexion(configuration);
        return await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1) FROM Productos
            WHERE empresaID = @EmpresaId AND LOWER(LTRIM(RTRIM(sku))) = LOWER(LTRIM(RTRIM(@Sku)))",
            new { EmpresaId = empresaId, Sku = sku }) > 0;
    }

    public static async Task<int> IAProductoDuplicadoSkuAsync(IConfiguration configuration, int empresaId, string sku, SqlTransaction? transaction = null)
    {
        var connection = transaction?.Connection as SqlConnection;
        if (connection is null)
        {
            using var nueva = (SqlConnection)CrearConexion(configuration);
            return await nueva.ExecuteScalarAsync<int>(@"
                SELECT COUNT(1) FROM Productos
                WHERE empresaID = @EmpresaId AND LOWER(LTRIM(RTRIM(sku))) = LOWER(LTRIM(RTRIM(@Sku)))",
                new { EmpresaId = empresaId, Sku = sku });
        }

        return await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1) FROM Productos
            WHERE empresaID = @EmpresaId AND LOWER(LTRIM(RTRIM(sku))) = LOWER(LTRIM(RTRIM(@Sku)))",
            new { EmpresaId = empresaId, Sku = sku }, transaction);
    }

    public static async Task<int> IAContarVentasProductoAsync(IConfiguration configuration, int empresaId, int productoId, SqlTransaction? transaction = null)
    {
        var connection = transaction?.Connection as SqlConnection;
        if (connection is null)
        {
            using var nueva = (SqlConnection)CrearConexion(configuration);
            return await nueva.ExecuteScalarAsync<int>(@"
                SELECT COUNT(1)
                FROM DetalleVenta d
                INNER JOIN Ventas v ON v.id = d.ventaID
                WHERE v.empresaID = @EmpresaId AND d.productoID = @ProductoId",
                new { EmpresaId = empresaId, ProductoId = productoId });
        }

        return await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1)
            FROM DetalleVenta d
            INNER JOIN Ventas v ON v.id = d.ventaID
            WHERE v.empresaID = @EmpresaId AND d.productoID = @ProductoId",
            new { EmpresaId = empresaId, ProductoId = productoId }, transaction);
    }

    public static async Task<int> IACrearProductoAsync(IConfiguration configuration, int empresaId, string nombre, string sku,
        string? categoria, string? descripcion, decimal precio, int stock, int stockMinimo,
        string? proveedor, decimal costo, SqlTransaction transaction)
    {
        var connection = (SqlConnection)transaction.Connection;
        var producto = new Producto
        {
            EmpresaID = empresaId,
            Nombre = nombre,
            Sku = sku,
            Categoria = string.IsNullOrWhiteSpace(categoria) ? "Sin categoría" : categoria,
            Descripcion = descripcion,
            PrecioVenta = precio,
            StockActual = stock,
            StockMinimo = stockMinimo,
            Proveedor = proveedor,
            CostoCompra = costo,
            Activo = true,
            FechaCarga = DateTime.Now
        };

        producto.CategoriaID = ObtenerOCrearCategoriaId(connection, empresaId, producto.Categoria, transaction);
        producto.ProveedorID = ObtenerOCrearProveedorId(connection, empresaId, producto.Proveedor, transaction);

        var id = await connection.QuerySingleAsync<int>(@"
            INSERT INTO Productos (empresaID, categoriaID, proveedorID, nombre, sku, descripcion, fechaCarga, precioVenta, costoCompra, activo)
            VALUES (@EmpresaID, @CategoriaID, @ProveedorID, @Nombre, @Sku, @Descripcion, @FechaCarga, @PrecioVenta, @CostoCompra, @Activo);
            SELECT CAST(SCOPE_IDENTITY() AS int)", producto, transaction);

        await connection.ExecuteAsync(@"
            INSERT INTO Stock (productoID, cantidad, cantidadMinimoAlerta, activo, actualizadoEn)
            VALUES (@ProductoId, @Cantidad, @CantidadMinimo, 1, @Ahora)",
            new { ProductoId = id, Cantidad = stock, CantidadMinimo = stockMinimo, Ahora = DateTime.Now }, transaction);
        return id;
    }

    public static async Task<bool> IAEliminarProductoAsync(IConfiguration configuration, int empresaId, int productoId, SqlTransaction transaction)
    {
        var connection = (SqlConnection)transaction.Connection;
        await connection.ExecuteAsync("DELETE FROM Stock WHERE productoID = @ProductoId", new { ProductoId = productoId }, transaction);
        var filas = await connection.ExecuteAsync("DELETE FROM Productos WHERE id = @ProductoId AND empresaID = @EmpresaId",
            new { ProductoId = productoId, EmpresaId = empresaId }, transaction);
        return filas == 1;
    }

    public static async Task<bool> IAActualizarProductoPrecioAsync(IConfiguration configuration, int empresaId, int productoId, decimal precio, SqlTransaction transaction)
    {
        var connection = (SqlConnection)transaction.Connection;
        return await connection.ExecuteAsync(@"
            UPDATE Productos SET precioVenta = @Precio
            WHERE id = @ProductoId AND empresaID = @EmpresaId",
            new { Precio = precio, ProductoId = productoId, EmpresaId = empresaId }, transaction) == 1;
    }

    public static async Task<bool> IAActualizarProductoStockAsync(IConfiguration configuration, int empresaId, int productoId, int stock, SqlTransaction transaction)
    {
        var connection = (SqlConnection)transaction.Connection;
        var filas = await connection.ExecuteAsync(@"
            UPDATE Stock
            SET cantidad = @Stock, actualizadoEn = @Ahora
            WHERE productoID = @ProductoId",
            new { Stock = stock, Ahora = DateTime.Now, ProductoId = productoId }, transaction);
        if (filas > 0) return true;

        return await connection.ExecuteAsync(@"
            INSERT INTO Stock (productoID, cantidad, cantidadMinimoAlerta, activo, actualizadoEn)
            SELECT @ProductoId, @Stock, cantidadMinimoAlerta, 1, @Ahora
            FROM Stock WHERE 1 = 0",
            new { ProductoId = productoId, Stock = stock, Ahora = DateTime.Now }, transaction) >= 0;
    }

    public static async Task IAInsertarAuditoriaAsync(IConfiguration configuration, int usuarioId, int empresaId, string accion,
        int productoId, string antes, string despues, SqlTransaction transaction)
    {
        var connection = (SqlConnection)transaction.Connection;
        await connection.ExecuteAsync(@"
            INSERT INTO IAAuditoria (UsuarioId, EmpresaId, Accion, ProductoId, Antes, Despues, Fecha)
            VALUES (@UsuarioId, @EmpresaId, @Accion, @ProductoId, @Antes, @Despues, @Fecha)",
            new
            {
                UsuarioId = usuarioId,
                EmpresaId = empresaId,
                Accion = accion,
                ProductoId = productoId,
                Antes = antes,
                Despues = despues,
                Fecha = DateTime.UtcNow
            }, transaction);
    }

    public static bool EliminarReporte(IConfiguration configuration, int reporteId, int usuarioId, int empresaId)
    {
        if (reporteId <= 0 || usuarioId <= 0 || empresaId <= 0)
        {
            return false;
        }

        using SqlConnection connection = (SqlConnection)CrearConexion(configuration);
        string sql = @"
            DELETE FROM Reportes
            WHERE id = @ReporteId AND usuarioID = @UsuarioId AND empresaID = @EmpresaId";

        int afectados = connection.Execute(sql,
            new { ReporteId = reporteId, UsuarioId = usuarioId, EmpresaId = empresaId });
        return afectados > 0;
    }
}
