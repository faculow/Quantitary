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

    public static Producto AgregarProducto(IConfiguration configuration, Producto producto)
    {
        if (string.IsNullOrWhiteSpace(producto.Nombre) || string.IsNullOrWhiteSpace(producto.Sku))
        {
            throw new ArgumentException("El nombre y el SKU son obligatorios.");
        }

        producto.Nombre = producto.Nombre.Trim();
        producto.Sku = producto.Sku.Trim();
        producto.Proveedor = producto.Proveedor.Trim();
        producto.Categoria = string.IsNullOrWhiteSpace(producto.Categoria) ? "Sin categoría" : producto.Categoria.Trim();

        if (producto.PrecioVenta <= 0)
        {
            throw new ArgumentException($"El producto '{producto.Nombre}' tiene un precio inválido.");
        }

        using var connection = CrearConexion(configuration);

        producto.CategoriaID = ObtenerOCrearCategoriaId(connection, producto.EmpresaID, producto.Categoria);

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
        producto.Proveedor = producto.Proveedor.Trim();
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

        producto.CategoriaID = ObtenerOCrearCategoriaId(connection, empresaId.Value, producto.Categoria);

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

        connection.Execute(@"UPDATE Stock
                            SET cantidad = @Cantidad,
                                cantidadMinimoAlerta = @CantidadMinimoAlerta,
                                actualizadoEn = @ActualizadoEn
                            WHERE productoID = @ProductoId",
            new
            {
                Cantidad = producto.StockActual,
                CantidadMinimoAlerta = producto.StockMinimo,
                ActualizadoEn = DateTime.Now,
                ProductoId = id
            });

        return filasAfectadas > 0;
    }

    public static bool EliminarProducto(IConfiguration configuration, int id)
    {
        using var connection = CrearConexion(configuration);
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

    public static bool Registrarse(Usuario usuario, IConfiguration configuration)
    {
        var mailNormalizado = NormalizarMail(usuario.Mail);

        if (string.IsNullOrWhiteSpace(usuario.NombreUsuario)
            || string.IsNullOrWhiteSpace(mailNormalizado)
            || string.IsNullOrWhiteSpace(usuario.Contrasena))
        {
            return false;
        }

        using var connection = CrearConexion(configuration);
        connection.Open();

        int? empresaId = usuario.EmpresaID > 0 ? usuario.EmpresaID : null;
        int? rolId = usuario.RolID > 0 ? usuario.RolID : null;

        if (empresaId == null)
        {
            var nombreEmpresa = !string.IsNullOrWhiteSpace(usuario.NombreEmpresa)
                ? usuario.NombreEmpresa.Trim()
                : $"Empresa de {usuario.NombreUsuario.Trim()}";

            var empresaInsertada = connection.ExecuteScalar<int>(
                "INSERT INTO Empresa (nombre) VALUES (@Nombre); SELECT CAST(SCOPE_IDENTITY() as int);",
                new { Nombre = nombreEmpresa });
            empresaId = empresaInsertada;
        }

        if (rolId == null)
        {
            rolId = connection.QuerySingleOrDefault<int?>("SELECT TOP 1 id FROM Rol ORDER BY id");
        }

        if (empresaId == null || rolId == null)
        {
            return false;
        }

        var existe = connection.QuerySingleOrDefault<int>(
            "SELECT 1 FROM Usuarios WHERE LOWER(LTRIM(RTRIM(mail))) = @Mail",
            new { Mail = mailNormalizado.ToLowerInvariant() });

        if (existe == 1)
        {
            return false;
        }

        var query = @"
            INSERT INTO Usuarios (nombreUsuario, contrasena, mail, numeroTelefono, empresaID, rolID)
            VALUES (@NombreUsuario, @Contrasena, @Mail, @NumeroTelefono, @EmpresaID, @RolID)";

        connection.Execute(query, new
        {
            NombreUsuario = usuario.NombreUsuario.Trim(),
            Contrasena = usuario.Contrasena,
            Mail = mailNormalizado,
            NumeroTelefono = usuario.NumeroTelefono,
            EmpresaID = empresaId.Value,
            RolID = rolId.Value
        });

        usuario.EmpresaID = empresaId.Value;
        usuario.RolID = rolId.Value;

        return true;
    }

    public static bool IniciarSesion(string mail, string contrasena, HttpContext httpContext, IConfiguration configuration)
    {
        var mailNormalizado = NormalizarMail(mail);

        using var connection = CrearConexion(configuration);
        connection.Open();

        var usuario = connection.QuerySingleOrDefault<Usuario>(
            "SELECT TOP 1 * FROM Usuarios WHERE LOWER(LTRIM(RTRIM(mail))) = @Mail AND contrasena = @Contrasena",
            new { Mail = mailNormalizado.ToLowerInvariant(), Contrasena = contrasena });

        if (usuario == null)
        {
            return false;
        }

        var empresaIdForzado = mailNormalizado.Equals("admin@quantitary.com", StringComparison.OrdinalIgnoreCase) ? 1 : usuario.EmpresaID;
        if (empresaIdForzado <= 0)
        {
            empresaIdForzado = connection.QuerySingleOrDefault<int?>(
                "SELECT empresaID FROM Usuarios WHERE id = @UsuarioId",
                new { UsuarioId = usuario.Id }) ?? 0;
        }

        if (empresaIdForzado <= 0)
        {
            empresaIdForzado = connection.QuerySingleOrDefault<int?>(
                "SELECT TOP 1 id FROM Empresa ORDER BY id") ?? 0;
        }

        if (empresaIdForzado > 0)
        {
            usuario.EmpresaID = empresaIdForzado;
            connection.Execute(
                "UPDATE Usuarios SET empresaID = @EmpresaId WHERE id = @UsuarioId AND empresaID <> @EmpresaId",
                new { UsuarioId = usuario.Id, EmpresaId = empresaIdForzado });
        }

        var empresa = connection.QuerySingleOrDefault<Empresa>(
            "SELECT e.* FROM Empresa e INNER JOIN Usuarios u ON u.empresaID = e.id WHERE u.id = @UsuarioId",
            new { UsuarioId = usuario.Id });

        httpContext.Session.SetInt32("UsuarioID", usuario.Id);
        httpContext.Session.SetString("NombreUsuario", usuario.NombreUsuario);
        httpContext.Session.SetString("NombreEmpresa", empresa?.Nombre ?? $"Empresa de {usuario.NombreUsuario}");
        httpContext.Session.SetInt32("EmpresaID", usuario.EmpresaID);
        httpContext.Session.SetInt32("RolID", usuario.RolID);

        return true;
    }

    public static void CerrarSesion(HttpContext httpContext)
    {
        httpContext.Session.Clear();
    }

    public static int? ObtenerUsuarioLogueadoID(HttpContext httpContext)
    {
        return httpContext.Session.GetInt32("UsuarioID");
    }

    public static string? ObtenerNombreUsuario(HttpContext httpContext)
    {
        return httpContext.Session.GetString("NombreUsuario");
    }

    public static int ObtenerEmpresaIdActual(IConfiguration configuration, HttpContext httpContext)
    {
        var empresaId = httpContext.Session.GetInt32("EmpresaID");
        if (empresaId is > 0)
        {
            return empresaId.Value;
        }

        var usuarioId = httpContext.Session.GetInt32("UsuarioID");
        if (usuarioId is > 0)
        {
            using var connection = CrearConexion(configuration);
            var empresa = connection.QuerySingleOrDefault<int?>(
                "SELECT empresaID FROM Usuarios WHERE id = @UsuarioId",
                new { UsuarioId = usuarioId.Value });

            if (empresa is > 0)
            {
                httpContext.Session.SetInt32("EmpresaID", empresa.Value);
                return empresa.Value;
            }
        }

        using (var connection = CrearConexion(configuration))
        {
            var empresaDefault = connection.QuerySingleOrDefault<int?>(
                "SELECT TOP 1 id FROM Empresa ORDER BY id");

            if (empresaDefault is > 0)
            {
                httpContext.Session.SetInt32("EmpresaID", empresaDefault.Value);
                return empresaDefault.Value;
            }
        }

        return 1;
    }

    private static List<ChartPoint> ObtenerVentasPorPeriodo(IConfiguration configuration, int? empresaId, DateTime fecha, string range, string? nombreProducto = null, string? sku = null, string? categoria = null, DateTime? fechaInicio = null, DateTime? fechaFin = null)
    {
        var rangoNormalizado = (range ?? "todos").Trim();
        var (inicioRango, finRango) = NormalizarRangoFechas(rangoNormalizado, fechaInicio ?? fecha, fechaFin);
        var inicio = inicioRango.Date;
        var fin = finRango.Date;

        if (fin < inicio)
        {
            var intercambio = inicio;
            inicio = fin;
            fin = intercambio;
        }

                decimal ObtenerTotal(DateTime desde, DateTime hasta)
        {
            using var connection = CrearConexion(configuration);
            return connection.QuerySingleOrDefault<decimal?>(
                @"SELECT COALESCE(SUM(d.precioVenta), 0)
                  FROM DetalleVenta d
                  INNER JOIN Ventas v ON v.id = d.ventaID
                  INNER JOIN Productos p ON p.id = d.productoID
                  LEFT JOIN Categoria c ON c.id = p.categoriaID
                                    WHERE (@EmpresaId IS NULL OR p.empresaID = @EmpresaId)
                    AND v.fecha >= @Desde AND v.fecha <= @Hasta
                    AND (@NombreProducto IS NULL OR LOWER(LTRIM(RTRIM(p.nombre))) LIKE '%' + LOWER(LTRIM(RTRIM(@NombreProducto))) + '%')
                    AND (@Sku IS NULL OR LOWER(LTRIM(RTRIM(p.sku))) LIKE '%' + LOWER(LTRIM(RTRIM(@Sku))) + '%')
                    AND (@Categoria IS NULL OR LOWER(LTRIM(RTRIM(c.nombre))) = LOWER(LTRIM(RTRIM(@Categoria))))",
                new
                {
                    EmpresaId = empresaId,
                    Desde = desde,
                    Hasta = hasta,
                    NombreProducto = string.IsNullOrWhiteSpace(nombreProducto) ? null : nombreProducto.Trim(),
                    Sku = string.IsNullOrWhiteSpace(sku) ? null : sku.Trim(),
                    Categoria = string.IsNullOrWhiteSpace(categoria) ? null : categoria.Trim()
                }) ?? 0m;
        }

        if (rangoNormalizado.Equals("dia", StringComparison.OrdinalIgnoreCase))
        {
            var result = new List<ChartPoint>();
            var diaActual = inicio;

            while (diaActual <= fin)
            {
                var desde = diaActual.Date;
                var hasta = diaActual.Date.AddDays(1).AddTicks(-1);
                result.Add(new ChartPoint
                {
                    Label = desde.ToString("dd/MM/yyyy"),
                    Valor = ObtenerTotal(desde, hasta)
                });

                diaActual = diaActual.AddDays(1);
            }

            return result;
        }

        if (rangoNormalizado.Equals("semana", StringComparison.OrdinalIgnoreCase))
        {
            var result = new List<ChartPoint>();
            var semanaInicio = inicio;

            while (semanaInicio <= fin)
            {
                var semanaFin = semanaInicio.AddDays(6) > fin ? fin : semanaInicio.AddDays(6);
                result.Add(new ChartPoint
                {
                    Label = $"{semanaInicio:dd/MM} - {semanaFin:dd/MM}",
                    Valor = ObtenerTotal(semanaInicio.Date, semanaFin.Date.AddDays(1).AddTicks(-1))
                });

                semanaInicio = semanaFin.AddDays(1);
            }

            return result;
        }

        if (rangoNormalizado.Equals("mes", StringComparison.OrdinalIgnoreCase))
        {
            var result = new List<ChartPoint>();
            var mesActual = new DateTime(inicio.Year, inicio.Month, 1);
            var mesFin = new DateTime(fin.Year, fin.Month, 1);
            var cultura = new CultureInfo("es-ES");

            while (mesActual <= mesFin)
            {
                var mesDesde = new DateTime(mesActual.Year, mesActual.Month, 1);
                var mesHasta = new DateTime(mesActual.Year, mesActual.Month, DateTime.DaysInMonth(mesActual.Year, mesActual.Month), 23, 59, 59, 999);

                result.Add(new ChartPoint
                {
                    Label = mesDesde.ToString("MMM", cultura),
                    Valor = ObtenerTotal(mesDesde, mesHasta)
                });

                mesActual = mesActual.AddMonths(1);
            }

            return result;
        }

        if (rangoNormalizado.Equals("anio", StringComparison.OrdinalIgnoreCase) || rangoNormalizado.Equals("año", StringComparison.OrdinalIgnoreCase))
        {
            var result = new List<ChartPoint>();
            var anioActual = inicio.Year;
            var anioFinal = fin.Year;

            while (anioActual <= anioFinal)
            {
                var desde = new DateTime(anioActual, 1, 1);
                var hasta = new DateTime(anioActual, 12, 31, 23, 59, 59, 999);
                result.Add(new ChartPoint
                {
                    Label = anioActual.ToString(CultureInfo.InvariantCulture),
                    Valor = ObtenerTotal(desde, hasta)
                });

                anioActual++;
            }

            return result;
        }

        var resultFallback = new List<ChartPoint>();
        var mesActualFallback = new DateTime(inicio.Year, inicio.Month, 1);
        var mesFinFallback = new DateTime(fin.Year, fin.Month, 1);
        var culturaFallback = new CultureInfo("es-ES");

        while (mesActualFallback <= mesFinFallback)
        {
            var mesDesde = new DateTime(mesActualFallback.Year, mesActualFallback.Month, 1);
            var mesHasta = new DateTime(mesActualFallback.Year, mesActualFallback.Month, DateTime.DaysInMonth(mesActualFallback.Year, mesActualFallback.Month), 23, 59, 59, 999);
            resultFallback.Add(new ChartPoint
            {
                Label = mesDesde.ToString("MMM", culturaFallback),
                Valor = ObtenerTotal(mesDesde, mesHasta)
            });

            mesActualFallback = mesActualFallback.AddMonths(1);
        }

        return resultFallback;
    }

    private static List<ChartPoint> ObtenerGananciaPorCategoria(IConfiguration configuration, int? empresaId, DateTime fecha, string range, string? nombreProducto = null, string? sku = null, string? categoria = null, DateTime? fechaInicio = null, DateTime? fechaFin = null)
    {
        var (inicio, fin) = NormalizarRangoFechas(range, fechaInicio ?? fecha, fechaFin);

        using var connection = CrearConexion(configuration);
        var rows = connection.Query<(string Nombre, decimal Valor)>(
            @"SELECT c.nombre AS Nombre, SUM(d.ganancia) AS Valor
              FROM DetalleVenta d
              INNER JOIN Ventas v ON v.id = d.ventaID
              INNER JOIN Productos p ON p.id = d.productoID
              LEFT JOIN Categoria c ON c.id = p.categoriaID
                            WHERE (@EmpresaId = 0 OR p.empresaID = @EmpresaId)
                AND v.fecha >= @Inicio AND v.fecha <= @Fin
                AND (@NombreProducto IS NULL OR LOWER(LTRIM(RTRIM(p.nombre))) LIKE '%' + LOWER(LTRIM(RTRIM(@NombreProducto))) + '%')
                AND (@Sku IS NULL OR LOWER(LTRIM(RTRIM(p.sku))) LIKE '%' + LOWER(LTRIM(RTRIM(@Sku))) + '%')
                AND (@Categoria IS NULL OR LOWER(LTRIM(RTRIM(c.nombre))) = LOWER(LTRIM(RTRIM(@Categoria))))
              GROUP BY c.nombre
              ORDER BY Valor DESC",
            new
            {
                EmpresaId = empresaId,
                Inicio = inicio,
                Fin = fin,
                NombreProducto = string.IsNullOrWhiteSpace(nombreProducto) ? null : nombreProducto.Trim(),
                Sku = string.IsNullOrWhiteSpace(sku) ? null : sku.Trim(),
                Categoria = string.IsNullOrWhiteSpace(categoria) ? null : categoria.Trim()
            }).ToList();

        return rows.Select(r => new ChartPoint
        {
            Label = r.Nombre,
            Valor = r.Valor
        }).ToList();
    }

    private static List<DashboardCategoryShare> ObtenerParticipacionVentas(IConfiguration configuration, int? empresaId, DateTime fecha, string range, string? nombreProducto = null, string? sku = null, string? categoria = null, DateTime? fechaInicio = null, DateTime? fechaFin = null)
    {
        var (inicio, fin) = NormalizarRangoFechas(range, fechaInicio ?? fecha, fechaFin);

        using var connection = CrearConexion(configuration);
        var rows = connection.Query<(string Nombre, decimal Valor)>(
            @"SELECT c.nombre AS Nombre, SUM(d.precioVenta) AS Valor
              FROM DetalleVenta d
              INNER JOIN Ventas v ON v.id = d.ventaID
              INNER JOIN Productos p ON p.id = d.productoID
              LEFT JOIN Categoria c ON c.id = p.categoriaID
                            WHERE (@EmpresaId = 0 OR p.empresaID = @EmpresaId)
                AND v.fecha >= @Inicio AND v.fecha <= @Fin
                AND (@NombreProducto IS NULL OR LOWER(LTRIM(RTRIM(p.nombre))) LIKE '%' + LOWER(LTRIM(RTRIM(@NombreProducto))) + '%')
                AND (@Sku IS NULL OR LOWER(LTRIM(RTRIM(p.sku))) LIKE '%' + LOWER(LTRIM(RTRIM(@Sku))) + '%')
                AND (@Categoria IS NULL OR LOWER(LTRIM(RTRIM(c.nombre))) = LOWER(LTRIM(RTRIM(@Categoria))))
              GROUP BY c.nombre
              ORDER BY Valor DESC",
            new
            {
                EmpresaId = empresaId,
                Inicio = inicio,
                Fin = fin,
                NombreProducto = string.IsNullOrWhiteSpace(nombreProducto) ? null : nombreProducto.Trim(),
                Sku = string.IsNullOrWhiteSpace(sku) ? null : sku.Trim(),
                Categoria = string.IsNullOrWhiteSpace(categoria) ? null : categoria.Trim()
            }).ToList();

        if (rows.Count == 0)
        {
            return new List<DashboardCategoryShare>();
        }

        var total = rows.Sum(r => r.Valor);

        return rows.Select(r =>
        {
            var porcentaje = total > 0 ? Math.Round((r.Valor / total) * 100m, 0, MidpointRounding.AwayFromZero) : 0m;

            return new DashboardCategoryShare
            {
                Nombre = r.Nombre,
                Valor = porcentaje
            };
        }).ToList();
    }

    public sealed class ProductoIndicadorBase
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
        public decimal CostoCompra { get; set; }
        public int EmpresaId { get; set; }
    }

    public sealed class ProductoVentaAggregate
    {
        public int ProductoId { get; set; }
        public int CantidadVendida { get; set; }
        public decimal VentasTotales { get; set; }
        public decimal CostoTotal { get; set; }
    }

    public sealed class ProductoCategoriaAggregate
    {
        public string Categoria { get; set; } = string.Empty;
        public decimal VentasTotales { get; set; }
    }

    public sealed class AlertaStockRegistro
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int Stock { get; set; }
        public int Minimo { get; set; }
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

    public static async Task<bool> TienePermisoEditarEmpresaAsync(IConfiguration configuration, int usuario, int empresa)
    {
        using var connection = CrearConexion(configuration);
        return await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1)
            FROM Usuarios u
            INNER JOIN Rol r ON r.id = u.rolID
            INNER JOIN Permisos p ON p.id = r.permisoID
            WHERE u.id = @usuario AND u.empresaID = @empresa AND p.gestionarUsuarios = 1",
            new { usuario, empresa }) == 1;
    }

    public static async Task<PermisosLectura?> ObtenerPermisosLecturaAsync(IConfiguration configuration, int usuarioId, int empresaId)
    {
        using var connection = CrearConexion(configuration);
        return await connection.QuerySingleOrDefaultAsync<PermisosLectura>(@"
            SELECT
                e.nombre AS NombreEmpresa,
                CAST(CASE WHEN p.verProductos = 1 THEN 1 ELSE 0 END AS bit) AS VerProductos,
                CAST(CASE WHEN p.verStock = 1 THEN 1 ELSE 0 END AS bit) AS VerStock,
                CAST(CASE WHEN p.verHistorialVentas = 1 THEN 1 ELSE 0 END AS bit) AS VerVentas,
                CAST(CASE WHEN p.verGanacia = 1 THEN 1 ELSE 0 END AS bit) AS VerGanancias
            FROM Usuarios u
            INNER JOIN Empresa e ON e.id = u.empresaID
            INNER JOIN Rol r ON r.id = u.rolID
            INNER JOIN Permisos p ON p.id = r.permisoID
            WHERE u.id = @UsuarioId
              AND u.empresaID = @EmpresaId",
            new { UsuarioId = usuarioId, EmpresaId = empresaId });
    }

    public static async Task<ResumenStock> ObtenerResumenStockAsync(IConfiguration configuration, int empresaId)
    {
        using var connection = CrearConexion(configuration);
        return await connection.QuerySingleOrDefaultAsync<ResumenStock>(@"
            SELECT
                COUNT(*) AS TotalProductos,
                SUM(CASE WHEN ISNULL(s.cantidad, 0) <= 0 THEN 1 ELSE 0 END) AS Agotados,
                SUM(CASE WHEN ISNULL(s.cantidad, 0) > 0 AND ISNULL(s.cantidad, 0) <= ISNULL(s.cantidadMinimoAlerta, 0) THEN 1 ELSE 0 END) AS BajoMinimo
            FROM Productos p
            LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
            WHERE p.empresaID = @EmpresaId",
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
            ORDER BY
                CASE WHEN @IncluirStockBajo = 1 AND (ISNULL(s.cantidad, 0) <= ISNULL(s.cantidadMinimoAlerta, 0) OR ISNULL(s.cantidad, 0) = 0) THEN 0 ELSE 1 END,
                ISNULL(s.cantidad, 0) ASC,
                p.nombre ASC";

        return (await connection.QueryAsync<ProductoLectura>(sql, new { EmpresaId = empresaId, IncluirStockBajo = incluirStockBajo })).ToList();
    }

    public static async Task<int> ExisteTablaVentasAsync(IConfiguration configuration)
    {
        using var connection = CrearConexion(configuration);
        return await connection.ExecuteScalarAsync<int>(@"
            SELECT CASE WHEN OBJECT_ID('dbo.Ventas', 'U') IS NOT NULL THEN 1 ELSE 0 END");
    }

    public static async Task<List<VentaDiaria>> ObtenerVentasUltimos30DiasAsync(IConfiguration configuration, int empresaId)
    {
        using var connection = CrearConexion(configuration);
        return (await connection.QueryAsync<VentaDiaria>(@"
            SELECT
                CAST(v.fecha AS date) AS Fecha,
                COUNT(*) AS Operaciones,
                SUM(d.cantidad) AS Unidades,
                SUM(d.precioVenta) AS Total,
                SUM(d.costoCompra) AS Costo
            FROM Ventas v
            INNER JOIN DetalleVenta d ON d.ventaID = v.id
            WHERE v.empresaID = @EmpresaId
              AND v.fecha >= DATEADD(day, -30, CAST(GETDATE() AS date))
            GROUP BY CAST(v.fecha AS date)
            ORDER BY CAST(v.fecha AS date) DESC",
            new { EmpresaId = empresaId })).ToList();
    }

    public static Task<List<ProductoVentaIA>> ObtenerRankingProductosUltimos30DiasAsync(IConfiguration configuration, int empresaId) =>
        ObtenerRankingProductosAsync(configuration, empresaId, 30);

    /// <summary>
    /// Ranking real de productos vendidos. Incluye el esquema actual (DetalleVenta) y,
    /// cuando existe, ventas antiguas guardadas solamente en la tabla Ventas.
    /// Un valor nulo en ultimosDias consulta todo el historial de la empresa.
    /// </summary>
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
            ? @"
                UNION ALL
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
            WITH Movimientos AS
            (
                SELECT
                    d.productoID AS ProductoId,
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
            Totales AS
            (
                SELECT
                    ProductoId,
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
            ORDER BY t.Unidades DESC, t.Facturacion DESC";

        return (await connection.QueryAsync<ProductoVentaIA>(sql, new { EmpresaId = empresaId, Desde = desde })).ToList();
    }

    public static async Task<bool> IAUsuarioValidoAsync(IConfiguration configuration, int usuario, int empresa)
    {
        using var connection = CrearConexion(configuration);
        return await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1)
            FROM Usuarios
            WHERE id = @Usuario
              AND empresaID = @Empresa",
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
            ORDER BY ActualizadoEn DESC, Titulo ASC",
            new { Usuario = usuario, Empresa = empresa })).ToList();
    }

    public static async Task<Guid> IACrearChatAsync(IConfiguration configuration, int usuario, int empresa)
    {
        var chatId = Guid.NewGuid();
        using var connection = CrearConexion(configuration);
        await connection.ExecuteAsync(@"
            INSERT INTO IAChats (Id, UsuarioId, EmpresaId, Titulo, CreadoEn, ActualizadoEn)
            VALUES (@Id, @Usuario, @Empresa, @Titulo, SYSUTCDATETIME(), SYSUTCDATETIME())",
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
              AND EmpresaId = @Empresa",
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
            ORDER BY m.Id ASC",
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
              AND EmpresaId = @Empresa",
            new
            {
                ChatId = chat,
                Usuario = usuario,
                Empresa = empresa,
                Pregunta = pregunta,
                Respuesta = respuesta,
                ContenidoJson = contenidoJson,
                TituloSugerido = tituloNormalizado
            });
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
              AND EmpresaId = @Empresa",
            new { ChatId = chat, Usuario = usuario, Empresa = empresa, Titulo = titulo.Trim() }) > 0;
    }

    public static async Task<bool> IAEliminarChatAsync(IConfiguration configuration, Guid chat, int usuario, int empresa)
    {
        using var connection = CrearConexion(configuration);
        return await connection.ExecuteAsync(@"
            DELETE FROM IAChats
            WHERE Id = @ChatId
              AND UsuarioId = @Usuario
              AND EmpresaId = @Empresa",
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
        var sql = $@"
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM Usuarios u
                INNER JOIN Rol r ON r.id = u.rolID
                INNER JOIN Permisos p ON p.id = r.permisoID
                WHERE u.id = @Usuario
                  AND u.empresaID = @Empresa
                  AND {columna} = 1
            ) THEN 1 ELSE 0 END";

        return await connection.ExecuteScalarAsync<int>(sql, new { Usuario = usuario, Empresa = empresa }) == 1;
    }

    public static async Task<List<AlertaStockRegistro>> ObtenerAlertasStockAsync(IConfiguration configuration, int empresaId)
    {
        using var connection = CrearConexion(configuration);
        return (await connection.QueryAsync<AlertaStockRegistro>(@"
            SELECT
                p.id AS Id,
                p.nombre AS Nombre,
                p.sku AS Sku,
                ISNULL(s.cantidad, 0) AS Stock,
                ISNULL(s.cantidadMinimoAlerta, 0) AS Minimo
            FROM Productos p
            LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
            WHERE p.empresaID = @EmpresaId
            ORDER BY ISNULL(s.cantidad, 0) ASC, p.nombre ASC",
            new { EmpresaId = empresaId })).ToList();
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
              AND LOWER(LTRIM(RTRIM(sku))) = LOWER(LTRIM(RTRIM(@Sku)))",
            new { EmpresaId = empresaId, Sku = sku.Trim() }) > 0;
    }

    public static async Task<List<IAProductoSql.Item>> IABuscarProductoPorSkuAsync(IConfiguration configuration, int empresaId, string sku, IDbTransaction? transaction = null)
    {
        using var connection = transaction == null ? CrearConexion(configuration) : transaction.Connection!;
        var sql = @"
            SELECT
                p.id AS Id,
                p.nombre AS Nombre,
                p.sku AS Sku,
                p.precioVenta AS Precio,
                ISNULL(s.cantidad, 0) AS Stock
            FROM Productos p
            LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
            WHERE p.empresaID = @EmpresaId
              AND LOWER(LTRIM(RTRIM(p.sku))) = LOWER(LTRIM(RTRIM(@Sku)))
            ORDER BY p.id";

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
              AND LOWER(LTRIM(RTRIM(sku))) = LOWER(LTRIM(RTRIM(@Sku)))",
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
              AND empresaID = @EmpresaId",
            new { EmpresaId = empresaId, ProductoId = productoId, Precio = precio }, transaction) > 0;
    }

    public static async Task<bool> IAActualizarProductoStockAsync(IConfiguration configuration, int empresaId, int productoId, int stock, IDbTransaction? transaction = null)
    {
        using var connection = transaction == null ? CrearConexion(configuration) : transaction.Connection!;
        var existe = await connection.QuerySingleOrDefaultAsync<int?>(@"
            SELECT TOP 1 id FROM Stock WHERE productoID = @ProductoId",
            new { ProductoId = productoId }, transaction);

        if (existe is null)
        {
            return await connection.ExecuteAsync(@"
                INSERT INTO Stock (productoID, cantidad, cantidadMinimoAlerta, activo, actualizadoEn)
                VALUES (@ProductoId, @Stock, 0, 1, SYSUTCDATETIME())",
                new { ProductoId = productoId, Stock = stock }, transaction) > 0;
        }

        return await connection.ExecuteAsync(@"
            UPDATE Stock
            SET cantidad = @Stock,
                actualizadoEn = SYSUTCDATETIME()
            WHERE productoID = @ProductoId
              AND EXISTS (SELECT 1 FROM Productos p WHERE p.id = @ProductoId AND p.empresaID = @EmpresaId)",
            new { EmpresaId = empresaId, ProductoId = productoId, Stock = stock }, transaction) > 0;
    }

    public static async Task<int> IAContarVentasProductoAsync(IConfiguration configuration, int empresaId, int productoId, IDbTransaction? transaction = null)
    {
        using var connection = transaction == null ? CrearConexion(configuration) : transaction.Connection!;
        return await connection.ExecuteScalarAsync<int>(@"
            SELECT CASE WHEN
                EXISTS (
                    SELECT 1
                    FROM DetalleVenta dv
                    INNER JOIN Ventas v ON v.id = dv.ventaID
                    WHERE dv.productoID = @ProductoId AND v.empresaID = @EmpresaId
                )
                OR EXISTS (
                    SELECT 1 FROM Ventas v
                    WHERE v.productoId = @ProductoId AND v.empresaID = @EmpresaId
                )
            THEN 1 ELSE 0 END",
            new { EmpresaId = empresaId, ProductoId = productoId }, transaction);
    }

    public static async Task<bool> IAEliminarProductoAsync(IConfiguration configuration, int empresaId, int productoId, IDbTransaction? transaction = null)
    {
        using var connection = transaction == null ? CrearConexion(configuration) : transaction.Connection!;
        return await connection.ExecuteAsync(@"
            DELETE FROM Productos
            WHERE id = @ProductoId
              AND empresaID = @EmpresaId",
            new { EmpresaId = empresaId, ProductoId = productoId }, transaction) > 0;
    }

    public static async Task IAInsertarAuditoriaAsync(IConfiguration configuration, int usuario, int empresa, string accion, int productoId, string antes, string despues, IDbTransaction? transaction = null)
    {
        using var connection = transaction == null ? CrearConexion(configuration) : transaction.Connection!;
        await connection.ExecuteAsync(@"
            INSERT INTO IAAuditoria (UsuarioId, EmpresaId, Accion, ProductoId, Antes, Despues, Fecha)
            VALUES (@UsuarioId, @EmpresaId, @Accion, @ProductoId, @Antes, @Despues, SYSUTCDATETIME())",
            new { UsuarioId = usuario, EmpresaId = empresa, Accion = accion, ProductoId = productoId, Antes = antes, Despues = despues }, transaction);
    }

    public sealed class IAChatResumen
    {
        public Guid Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public DateTime ActualizadoEn { get; set; }
    }

    public sealed class ProductoVentaIA
    {
        public string Nombre { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int Unidades { get; set; }
        public decimal Facturacion { get; set; }
        public decimal Ganancia { get; set; }
    }

    public sealed class IAMensaje
    {
        public string? Rol { get; set; }
        public string? Text { get; set; }
        public string? ContenidoJson { get; set; }
    }

    public static async Task<AnalisisVentasGananciasData> ObtenerAnalisisVentasGananciasAsync(IDbConnection connection, int empresaId, DateTime inicio, DateTime fin)
    {
        var inicioRango = inicio.Date;
        var finRangoExclusivo = fin.Date.AddDays(1);

        var sqlBase = @"
            SELECT COALESCE(SUM(d.precioVenta), 0)
            FROM DetalleVenta d
            INNER JOIN Ventas v ON v.id = d.ventaID
            INNER JOIN Productos p ON p.id = d.productoID
            WHERE (@EmpresaId = 0 OR v.empresaID = @EmpresaId)
              AND v.fecha >= @Inicio AND v.fecha < @FinExclusivo";

        var ventasTotales = await connection.QuerySingleAsync<decimal>(sqlBase, new { EmpresaId = empresaId, Inicio = inicioRango, FinExclusivo = finRangoExclusivo });

        var sqlGastos = @"
            SELECT COALESCE(SUM(d.costoCompra), 0)
            FROM DetalleVenta d
            INNER JOIN Ventas v ON v.id = d.ventaID
            INNER JOIN Productos p ON p.id = d.productoID
            WHERE (@EmpresaId = 0 OR v.empresaID = @EmpresaId)
              AND v.fecha >= @Inicio AND v.fecha < @FinExclusivo";

          var gastosTotales = await connection.QuerySingleAsync<decimal>(sqlGastos, new { EmpresaId = empresaId, Inicio = inicioRango, FinExclusivo = finRangoExclusivo });

        var sqlPromedio = @"
            SELECT COALESCE(AVG(d.precioVenta), 0)
            FROM DetalleVenta d
            INNER JOIN Ventas v ON v.id = d.ventaID
            INNER JOIN Productos p ON p.id = d.productoID
            WHERE (@EmpresaId = 0 OR v.empresaID = @EmpresaId)
              AND v.fecha >= @Inicio AND v.fecha < @FinExclusivo";

          var ventaPromedio = await connection.QuerySingleAsync<decimal>(sqlPromedio, new { EmpresaId = empresaId, Inicio = inicioRango, FinExclusivo = finRangoExclusivo });

        var sqlGanancias = @"
            SELECT COALESCE(SUM(d.ganancia), 0)
            FROM DetalleVenta d
            INNER JOIN Ventas v ON v.id = d.ventaID
            INNER JOIN Productos p ON p.id = d.productoID
            WHERE (@EmpresaId = 0 OR v.empresaID = @EmpresaId)
              AND v.fecha >= @Inicio AND v.fecha < @FinExclusivo";

          var ganancias = await connection.QuerySingleAsync<decimal>(sqlGanancias, new { EmpresaId = empresaId, Inicio = inicioRango, FinExclusivo = finRangoExclusivo });

        var sqlCantidadVentas = @"
            SELECT COALESCE(SUM(d.cantidad), 0)
            FROM DetalleVenta d
            INNER JOIN Ventas v ON v.id = d.ventaID
            INNER JOIN Productos p ON p.id = d.productoID
            WHERE (@EmpresaId = 0 OR v.empresaID = @EmpresaId)
              AND v.fecha >= @Inicio AND v.fecha < @FinExclusivo";

          var ventasTotalesCantidad = await connection.QuerySingleAsync<int>(sqlCantidadVentas, new { EmpresaId = empresaId, Inicio = inicioRango, FinExclusivo = finRangoExclusivo });

        var sqlVentasDiarias = @"
            SELECT CAST(v.fecha AS date) AS Label, COALESCE(SUM(d.precioVenta), 0) AS Valor
            FROM DetalleVenta d
            INNER JOIN Ventas v ON v.id = d.ventaID
            INNER JOIN Productos p ON p.id = d.productoID
            WHERE (@EmpresaId = 0 OR v.empresaID = @EmpresaId)
                            AND v.fecha >= @Inicio AND v.fecha < @FinExclusivo
            GROUP BY CAST(v.fecha AS date)
            ORDER BY CAST(v.fecha AS date)";

                var ventasDiarias = (await connection.QueryAsync<ChartPoint>(sqlVentasDiarias, new { EmpresaId = empresaId, Inicio = inicioRango, FinExclusivo = finRangoExclusivo })).ToList();

        var ventaPorSemana = new List<ChartPoint>();
        var semanaActual = inicio.Date;

        while (semanaActual <= fin.Date)
        {
            var semanaFin = semanaActual.AddDays(6) > fin.Date ? fin.Date : semanaActual.AddDays(6);
            var sqlVentaSemana = @"
                SELECT COALESCE(SUM(d.precioVenta), 0)
                FROM DetalleVenta d
                INNER JOIN Ventas v ON v.id = d.ventaID
                INNER JOIN Productos p ON p.id = d.productoID
                WHERE (@EmpresaId = 0 OR v.empresaID = @EmpresaId)
                  AND v.fecha >= @SemanaInicio AND v.fecha <= @SemanaFin";

            var totalSemana = await connection.QuerySingleAsync<decimal>(sqlVentaSemana, new
            {
                EmpresaId = empresaId,
                SemanaInicio = semanaActual.Date,
                SemanaFin = semanaFin.Date.AddDays(1).AddTicks(-1)
            });

            ventaPorSemana.Add(new ChartPoint
            {
                Label = $"{semanaActual:dd/MM} - {semanaFin:dd/MM}",
                Valor = totalSemana
            });

            semanaActual = semanaActual.AddDays(7);
        }

        var sqlGananciaPorCategoria = @"
            SELECT c.nombre AS Label, COALESCE(SUM(d.ganancia), 0) AS Valor
            FROM DetalleVenta d
            INNER JOIN Ventas v ON v.id = d.ventaID
            INNER JOIN Productos p ON p.id = d.productoID
            LEFT JOIN Categoria c ON c.id = p.categoriaID
            WHERE (@EmpresaId = 0 OR v.empresaID = @EmpresaId)
                            AND v.fecha >= @Inicio AND v.fecha < @FinExclusivo
            GROUP BY c.nombre
            ORDER BY Valor DESC";

                var gananciaPorCategoria = (await connection.QueryAsync<ChartPoint>(sqlGananciaPorCategoria, new { EmpresaId = empresaId, Inicio = inicioRango, FinExclusivo = finRangoExclusivo })).ToList();

        return new AnalisisVentasGananciasData
        {
            VentasTotales = ventasTotales,
            GastosTotales = gastosTotales,
            VentaPromedio = ventaPromedio,
            Ganancias = ganancias,
            VentasTotalesCantidad = ventasTotalesCantidad,
            VentasDiarias = ventasDiarias,
            VentaPorSemana = ventaPorSemana,
            GananciaPorCategoria = gananciaPorCategoria,
            MargenNeto = ventasTotales == 0 ? 0m : (ganancias / ventasTotales) * 100m,
            FormulaGanancia = ventasTotales - gastosTotales
        };
    }

    public static async Task<AnalisisProductoData> ObtenerAnalisisProductoAsync(IDbConnection connection, int empresaId, DateTime inicio, DateTime fin)
    {
        var inicioRango = inicio.Date;
        var finRangoExclusivo = fin.Date.AddDays(1);

        var productos = (await connection.QueryAsync<AnalisisProductoRow>(@"
            SELECT p.id AS Id,
                   p.nombre AS Nombre,
                   p.sku AS Sku,
                   c.nombre AS Categoria,
                   p.precioVenta AS Precio,
                   p.costoCompra AS CostoCompra,
                   ISNULL(s.cantidad, 0) AS StockActual,
                   ISNULL(s.cantidadMinimoAlerta, 0) AS StockMinimo,
                   0 AS CantidadVendida
            FROM Productos p
            LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
            LEFT JOIN Categoria c ON c.id = p.categoriaID
            WHERE (@EmpresaId = 0 OR p.empresaID = @EmpresaId)
            ORDER BY p.nombre", new { EmpresaId = empresaId })).ToList();

        var ventasPorProducto = (await connection.QueryAsync<AnalisisVentaProductoRow>(@"
            SELECT d.productoID AS ProductoId,
                   SUM(d.cantidad) AS CantidadVendida,
                   SUM(d.precioVenta) AS VentasTotales,
                   SUM(d.costoCompra) AS GastoTotal,
                   SUM(d.ganancia) AS GananciaTotal
            FROM DetalleVenta d
            INNER JOIN Ventas v ON v.id = d.ventaID
            INNER JOIN Productos p ON p.id = d.productoID
            WHERE (@EmpresaId = 0 OR p.empresaID = @EmpresaId)
            GROUP BY d.productoID",
            new { EmpresaId = empresaId })).ToList();

        var ventasPorMes = (await connection.QueryAsync<dynamic>(@"
            SELECT DATENAME(month, v.fecha) AS Mes,
                   MONTH(v.fecha) AS MesNumero,
                   YEAR(v.fecha) AS Anio,
                   SUM(d.precioVenta) AS Total,
                   SUM(d.cantidad) AS Cantidad
            FROM DetalleVenta d
            INNER JOIN Ventas v ON v.id = d.ventaID
            INNER JOIN Productos p ON p.id = d.productoID
            WHERE (@EmpresaId = 0 OR p.empresaID = @EmpresaId)
                            AND v.fecha >= @Inicio AND v.fecha < @FinExclusivo
            GROUP BY YEAR(v.fecha), MONTH(v.fecha), DATENAME(month, v.fecha)
                        ORDER BY YEAR(v.fecha), MONTH(v.fecha)", new { EmpresaId = empresaId, Inicio = inicioRango, FinExclusivo = finRangoExclusivo })).ToList();

        var interesCompraPorMes = (await connection.QueryAsync<dynamic>(@"
            SELECT DATENAME(month, v.fecha) AS Mes,
                   MONTH(v.fecha) AS MesNumero,
                   YEAR(v.fecha) AS Anio,
                   COUNT(DISTINCT v.id) AS InteresCompra,
                   SUM(d.cantidad) AS Cantidad
            FROM DetalleVenta d
            INNER JOIN Ventas v ON v.id = d.ventaID
            INNER JOIN Productos p ON p.id = d.productoID
            WHERE (@EmpresaId = 0 OR p.empresaID = @EmpresaId)
                            AND v.fecha >= @Inicio AND v.fecha < @FinExclusivo
            GROUP BY YEAR(v.fecha), MONTH(v.fecha), DATENAME(month, v.fecha)
                        ORDER BY YEAR(v.fecha), MONTH(v.fecha)", new { EmpresaId = empresaId, Inicio = inicioRango, FinExclusivo = finRangoExclusivo })).ToList();

        var ventasSemana = (await connection.QueryAsync<dynamic>(@"
            SELECT DATENAME(weekday, v.fecha) AS Dia,
                   SUM(d.precioVenta) AS Total,
                   SUM(d.cantidad) AS Cantidad
            FROM DetalleVenta d
            INNER JOIN Ventas v ON v.id = d.ventaID
            INNER JOIN Productos p ON p.id = d.productoID
            WHERE (@EmpresaId = 0 OR p.empresaID = @EmpresaId)
                            AND v.fecha >= @Inicio AND v.fecha < @FinExclusivo
            GROUP BY DATENAME(weekday, v.fecha)
            ORDER BY CASE DATENAME(weekday, v.fecha)
                WHEN 'Monday' THEN 1
                WHEN 'Tuesday' THEN 2
                WHEN 'Wednesday' THEN 3
                WHEN 'Thursday' THEN 4
                WHEN 'Friday' THEN 5
                WHEN 'Saturday' THEN 6
                WHEN 'Sunday' THEN 7
                                ELSE 8 END", new { EmpresaId = empresaId, Inicio = inicioRango, FinExclusivo = finRangoExclusivo })).ToList();

        return new AnalisisProductoData
        {
            Productos = productos,
            VentasPorProducto = ventasPorProducto,
            VentasPorMes = ventasPorMes,
            InteresCompraPorMes = interesCompraPorMes,
            VentasSemana = ventasSemana
        };
    }

    public static async Task<IndicadoresYOportunidadesConsultaData> ObtenerIndicadoresYOportunidadesConsultaAsync(IDbConnection connection, int? empresaId = null)
    {
        var productos = connection.Query<ProductoIndicadorBase>(@"
            SELECT p.id AS Id,
                   p.nombre AS Nombre,
                   c.nombre AS Categoria,
                   p.precioVenta AS Precio,
                   ISNULL(s.cantidad, 0) AS StockActual,
                   ISNULL(s.cantidadMinimoAlerta, 0) AS StockMinimo,
                   p.costoCompra AS CostoCompra,
                   p.empresaID AS EmpresaId
            FROM Productos p
            LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
            LEFT JOIN Categoria c ON c.id = p.categoriaID
            WHERE (@EmpresaId = 0 OR p.empresaID = @EmpresaId)
            ORDER BY p.nombre",
            new { EmpresaId = empresaId == 0 ? null : empresaId }).ToList();

        var ventasPorProducto = connection.Query<ProductoVentaAggregate>(@"
            SELECT d.productoID AS ProductoId,
                   SUM(d.cantidad) AS CantidadVendida,
                   SUM(d.precioVenta) AS VentasTotales,
                   SUM(d.costoCompra) AS CostoTotal
            FROM DetalleVenta d
            INNER JOIN Ventas v ON v.id = d.ventaID
            INNER JOIN Productos p ON p.id = d.productoID
            WHERE (@EmpresaId = 0 OR p.empresaID = @EmpresaId)
            GROUP BY d.productoID",
            new { EmpresaId = empresaId == 0 ? null : empresaId }).ToList();

        var ventasPorCategoria = connection.Query<ProductoCategoriaAggregate>(@"
            SELECT c.nombre AS Categoria,
                   SUM(d.precioVenta) AS VentasTotales
            FROM DetalleVenta d
            INNER JOIN Ventas v ON v.id = d.ventaID
            INNER JOIN Productos p ON p.id = d.productoID
            LEFT JOIN Categoria c ON c.id = p.categoriaID
            WHERE (@EmpresaId = 0 OR p.empresaID = @EmpresaId)
            GROUP BY c.nombre",
            new { EmpresaId = empresaId == 0 ? null : empresaId }).ToList();

        return new IndicadoresYOportunidadesConsultaData
        {
            Productos = productos,
            VentasPorProducto = ventasPorProducto,
            VentasPorCategoria = ventasPorCategoria
        };
    }

    public static async Task<PerfilAjustesDatos?> ObtenerDatosAjustesAsync(IDbConnection connection, int usuario, int empresa)
    {
        return await connection.QuerySingleOrDefaultAsync<PerfilAjustesDatos>(@"
            SELECT u.nombreUsuario, u.mail, u.numeroTelefono, e.nombre AS NombreEmpresa,
                   CAST(CASE WHEN ISNULL(p.gestionarUsuarios, 0) = 1 THEN 1 ELSE 0 END AS bit) AS PuedeEditarEmpresa
            FROM Usuarios u
            INNER JOIN Empresa e ON e.id = u.empresaID
            LEFT JOIN Rol r ON r.id = u.rolID
            LEFT JOIN Permisos p ON p.id = r.permisoID
            WHERE u.id = @Usuario AND u.empresaID = @Empresa",
            new { Usuario = usuario, Empresa = empresa });
    }

    public static async Task<DatosRepetidos> ObtenerDatosRepetidosAsync(IDbConnection connection, string mail, string nombre, int usuarioActual)
    {
        return await connection.QuerySingleAsync<DatosRepetidos>(@"
            SELECT
                  COUNT(CASE WHEN LOWER(LTRIM(RTRIM(mail))) = @Mail THEN 1 END) AS Mail,
                  COUNT(CASE WHEN LOWER(LTRIM(RTRIM(nombreUsuario))) = LOWER(@Nombre) THEN 1 END) AS Nombre
              FROM Usuarios
              WHERE id <> @Usuario",
            new { Mail = mail, Nombre = nombre, Usuario = usuarioActual });
    }

    public static async Task<int> RegistroVentaAsync(IDbConnection connection, int productoId, string nombreProducto, string metodoPago, int cantidad, decimal precioVentaTotal, decimal precioCompraTotal, int usuarioId, int empresaId, IDbTransaction? transaction = null)
    {
        var parametros = new
        {
            ProductoId = productoId,
            NombreProducto = nombreProducto,
            EmpresaId = empresaId,
            UsuarioId = usuarioId,
            Fecha = DateTime.Now,
            MetodoPago = metodoPago,
            Cantidad = cantidad,
            Precio = precioVentaTotal,
            Costo = precioCompraTotal,
            TotalVenta = precioVentaTotal,
            Observacion = $"Venta de {cantidad} unidades de {nombreProducto}"
        };

        if (ExisteColumna(connection, "Ventas", "productoId"))
        {
            return await connection.ExecuteScalarAsync<int>(@"
                INSERT INTO Ventas
                    (productoId, nombreProducto, empresaID, usuarioID, fecha, metodoPago, cantidad, precio, costo, totalVenta, observacion)
                VALUES
                    (@ProductoId, @NombreProducto, @EmpresaId, @UsuarioId, @Fecha, @MetodoPago, @Cantidad, @Precio, @Costo, @TotalVenta, @Observacion);
                SELECT CAST(SCOPE_IDENTITY() as int);", parametros, transaction);
        }

        return await connection.ExecuteScalarAsync<int>(@"
            INSERT INTO Ventas (empresaID, usuarioID, fecha, metodoPago, totalVenta, observacion)
            VALUES (@EmpresaId, @UsuarioId, @Fecha, @MetodoPago, @TotalVenta, @Observacion);
            SELECT CAST(SCOPE_IDENTITY() as int);", parametros, transaction);
    }

    public static async Task<int> InsertarDetalleVentaAsync(IDbConnection connection, int ventaId, int productoId, int cantidad, decimal precioVentaTotal, decimal precioCompraTotal, decimal porcentajeGanancia, decimal ganancia, IDbTransaction? transaction = null)
    {
        var parametros = new
        {
            VentaId = ventaId,
            ProductoId = productoId,
            Cantidad = cantidad,
            PrecioVenta = precioVentaTotal,
            CostoCompra = precioCompraTotal,
            PorcentajeGanancia = porcentajeGanancia,
            Ganancia = ganancia
        };

        if (ExisteColumna(connection, "DetalleVenta", "precioCompra"))
        {
            return await connection.ExecuteAsync(@"
                INSERT INTO DetalleVenta
                    (ventaID, productoID, cantidad, precioVenta, precioCompra, costoCompra, porcentajeGanancia, ganancia)
                VALUES
                    (@VentaId, @ProductoId, @Cantidad, @PrecioVenta, @CostoCompra, @CostoCompra, @PorcentajeGanancia, @Ganancia)", parametros, transaction);
        }

        return await connection.ExecuteAsync(@"
            INSERT INTO DetalleVenta (ventaID, productoID, cantidad, precioVenta, costoCompra, porcentajeGanancia, ganancia)
            VALUES (@VentaId, @ProductoId, @Cantidad, @PrecioVenta, @CostoCompra, @PorcentajeGanancia, @Ganancia)", parametros, transaction);
    }

    public static async Task<int> TienePermisoEmpresaAsync(IDbConnection connection, int usuario, int empresa)
    {
        return await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1)
            FROM Usuarios u
            INNER JOIN Rol r ON r.id = u.rolID
            INNER JOIN Permisos p ON p.id = r.permisoID
            WHERE u.id = @usuario AND u.empresaID = @empresa AND p.gestionarUsuarios = 1",
            new { usuario, empresa });
    }

    public static async Task<string?> ObtenerContrasenaActualAsync(IDbConnection connection, int usuario, int empresa)
    {
        return await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT contrasena FROM Usuarios WHERE id = @Usuario AND empresaID = @Empresa",
           

            new { Usuario = usuario, Empresa = empresa });
    }

    public static async Task<int> GuardarPerfilAsync(IDbConnection connection, string nombre, string mail, string? telefono, int usuario, int empresa)
    {
        return await connection.ExecuteAsync(
            @"UPDATE Usuarios
              SET nombreUsuario = @Nombre, mail = @Mail, numeroTelefono = @Telefono
              WHERE id = @Usuario AND empresaID = @Empresa",
            new
            {
                Nombre = nombre,
                Mail = mail,
                Telefono = string.IsNullOrWhiteSpace(telefono) ? null : telefono.Trim(),
                Usuario = usuario,
                Empresa = empresa
            });
    }

    public static async Task<int> GuardarEmpresaAsync(IDbConnection connection, string nombre, int empresa)
    {
        return await connection.ExecuteAsync(
            "UPDATE Empresa SET nombre = @Nombre WHERE id = @Empresa",
            new { Nombre = nombre, Empresa = empresa });
    }

    public static async Task<int> CambiarContrasenaAsync(IDbConnection connection, string nueva, int usuario, int empresa)
    {
        return await connection.ExecuteAsync(
            "UPDATE Usuarios SET contrasena = @Nueva WHERE id = @Usuario AND empresaID = @Empresa",
            new { Nueva = nueva, Usuario = usuario, Empresa = empresa });
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

    public sealed class IndicadoresYOportunidadesConsultaData
    {
        public List<ProductoIndicadorBase> Productos { get; set; } = new();
        public List<ProductoVentaAggregate> VentasPorProducto { get; set; } = new();
        public List<ProductoCategoriaAggregate> VentasPorCategoria { get; set; } = new();
    }

    public sealed class PerfilAjustesDatos
    {
        public string NombreUsuario { get; set; } = string.Empty;
        public string Mail { get; set; } = string.Empty;
        public string? NumeroTelefono { get; set; }
        public string NombreEmpresa { get; set; } = string.Empty;
        public bool PuedeEditarEmpresa { get; set; }
    }

    public sealed class DatosRepetidos
    {
        public int Mail { get; set; }
        public int Nombre { get; set; }
    }

    private static bool ExisteTabla(IDbConnection connection, string nombreTabla)
    {
        var sql = "SELECT CASE WHEN OBJECT_ID(@NombreTabla, 'U') IS NOT NULL THEN 1 ELSE 0 END;";
        return connection.QuerySingle<int>(sql, new { NombreTabla = nombreTabla }) == 1;
    }

    private static bool ExisteColumna(IDbConnection connection, string nombreTabla, string nombreColumna)
    {
        var sql = @"SELECT CASE WHEN EXISTS (
                        SELECT 1
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_NAME = @NombreTabla
                          AND COLUMN_NAME = @NombreColumna
                          AND TABLE_SCHEMA = 'dbo') THEN 1 ELSE 0 END;";

        return connection.QuerySingle<int>(sql, new { NombreTabla = nombreTabla, NombreColumna = nombreColumna }) == 1;
    }

    public static void GarantizarEsquemaActual(IConfiguration configuration)
    {
        using var connection = CrearConexion(configuration);
        connection.Open();

        if (!ExisteTabla(connection, "dbo.Categoria"))
        {
            connection.Execute(@"CREATE TABLE dbo.Categoria (
                    id int IDENTITY(1,1) NOT NULL,
                    empresaID int NOT NULL,
                    nombre varchar(100) NOT NULL,
                    CONSTRAINT PK_Categoria PRIMARY KEY (id));");
        }

        if (!ExisteTabla(connection, "dbo.Proveedores"))
        {
            connection.Execute(@"CREATE TABLE dbo.Proveedores (
                    id int IDENTITY(1,1) NOT NULL,
                    empresaID int NOT NULL,
                    nombre varchar(150) NOT NULL,
                    telefono varchar(30) NULL,
                    mail varchar(150) NULL,
                    costoEnvio decimal(12,2) NOT NULL DEFAULT 0,
                    detalles varchar(max) NULL,
                    CONSTRAINT PK_Proveedores PRIMARY KEY (id));");
        }

        if (!ExisteTabla(connection, "dbo.Productos"))
        {
            connection.Execute(@"CREATE TABLE dbo.Productos (
                    id int IDENTITY(1,1) NOT NULL,
                    empresaID int NOT NULL,
                    categoriaID int NULL,
                    proveedorID int NULL,
                    nombre varchar(150) NOT NULL,
                    sku varchar(100) NOT NULL,
                    descripcion varchar(500) NULL,
                    fechaCarga datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
                    precioVenta decimal(12,2) NOT NULL DEFAULT 0,
                    costoCompra decimal(12,2) NOT NULL DEFAULT 0,
                    activo bit NOT NULL DEFAULT 1,
                    CONSTRAINT PK_Productos PRIMARY KEY (id));");
        }

        if (!ExisteTabla(connection, "dbo.Stock"))
        {
            connection.Execute(@"CREATE TABLE dbo.Stock (
                    id int IDENTITY(1,1) NOT NULL,
                    productoID int NOT NULL,
                    cantidad int NOT NULL DEFAULT 0,
                    cantidadMinimoAlerta int NOT NULL DEFAULT 0,
                    activo bit NOT NULL DEFAULT 1,
                    actualizadoEn datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT PK_Stock PRIMARY KEY (id));");
        }

        if (!ExisteTabla(connection, "dbo.Ventas"))
        {
            connection.Execute(@"CREATE TABLE dbo.Ventas (
                    id int IDENTITY(1,1) NOT NULL,
                    empresaID int NULL,
                    usuarioID int NULL,
                    fecha datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
                    metodoPago varchar(50) NOT NULL,
                    totalVenta decimal(12,2) NOT NULL DEFAULT 0,
                    observacion varchar(500) NULL,
                    CONSTRAINT PK_Ventas PRIMARY KEY (id));");
        }

        if (!ExisteTabla(connection, "dbo.DetalleVenta"))
        {
            connection.Execute(@"CREATE TABLE dbo.DetalleVenta (
                    id int IDENTITY(1,1) NOT NULL,
                    ventaID int NOT NULL,
                    productoID int NOT NULL,
                    cantidad int NOT NULL,
                    precioVenta decimal(12,2) NOT NULL,
                    costoCompra decimal(12,2) NOT NULL DEFAULT 0,
                    porcentajeGanancia decimal(10,2) NULL,
                    ganancia decimal(12,2) NOT NULL DEFAULT 0,
                    CONSTRAINT PK_DetalleVenta PRIMARY KEY (id));");
        }

        var columnasCategoria = new[]
        {
            ("empresaID", "ALTER TABLE dbo.Categoria ADD empresaID int NOT NULL DEFAULT 1;"),
            ("nombre", "ALTER TABLE dbo.Categoria ADD nombre varchar(100) NULL;")
        };

        foreach (var (columna, sql) in columnasCategoria)
        {
            if (!ExisteColumna(connection, "Categoria", columna))
            {
                connection.Execute(sql);
            }
        }

        var columnasProveedores = new[]
        {
            ("empresaID", "ALTER TABLE dbo.Proveedores ADD empresaID int NOT NULL DEFAULT 1;"),
            ("nombre", "ALTER TABLE dbo.Proveedores ADD nombre varchar(150) NULL;"),
            ("telefono", "ALTER TABLE dbo.Proveedores ADD telefono varchar(30) NULL;"),
            ("mail", "ALTER TABLE dbo.Proveedores ADD mail varchar(150) NULL;")
        };

        foreach (var (columna, sql) in columnasProveedores)
        {
            if (!ExisteColumna(connection, "Proveedores", columna))
            {
                connection.Execute(sql);
            }
        }

        if (ExisteColumna(connection, "Proveedores", "productoPrincipal"))
        {
            connection.Execute(@"UPDATE dbo.Proveedores
                                 SET nombre = COALESCE(NULLIF(LTRIM(RTRIM(nombre)), ''), NULLIF(LTRIM(RTRIM(productoPrincipal)), ''), 'Proveedor ' + CAST(id AS varchar(20)))
                                 WHERE nombre IS NULL OR LTRIM(RTRIM(nombre)) = '';");
        }
        else
        {
            connection.Execute(@"UPDATE dbo.Proveedores
                                 SET nombre = 'Proveedor ' + CAST(id AS varchar(20))
                                 WHERE nombre IS NULL OR LTRIM(RTRIM(nombre)) = '';");
        }

        var columnasProductos = new[]
        {
            ("empresaID", "ALTER TABLE dbo.Productos ADD empresaID int NOT NULL DEFAULT 1;"),
            ("nombre", "ALTER TABLE dbo.Productos ADD nombre varchar(150) NULL;"),
            ("sku", "ALTER TABLE dbo.Productos ADD sku varchar(100) NULL;"),
            ("categoriaID", "ALTER TABLE dbo.Productos ADD categoriaID int NULL;"),
            ("proveedorID", "ALTER TABLE dbo.Productos ADD proveedorID int NULL;"),
            ("precioVenta", "ALTER TABLE dbo.Productos ADD precioVenta decimal(12,2) NOT NULL DEFAULT 0;"),
            ("costoCompra", "ALTER TABLE dbo.Productos ADD costoCompra decimal(12,2) NOT NULL DEFAULT 0;"),
            ("fechaCarga", "ALTER TABLE dbo.Productos ADD fechaCarga datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME();"),
            ("activo", "ALTER TABLE dbo.Productos ADD activo bit NOT NULL DEFAULT 1;"),
            ("descripcion", "ALTER TABLE dbo.Productos ADD descripcion varchar(500) NULL;")
        };

        foreach (var (columna, sql) in columnasProductos)
        {
            if (!ExisteColumna(connection, "Productos", columna))
            {
                connection.Execute(sql);
            }
        }

        if (ExisteColumna(connection, "Productos", "nombreProducto"))
        {
            connection.Execute(@"UPDATE dbo.Productos
                                 SET nombre = COALESCE(NULLIF(LTRIM(RTRIM(nombre)), ''), nombreProducto)
                                 WHERE nombre IS NULL OR LTRIM(RTRIM(nombre)) = '';");
        }

        if (ExisteColumna(connection, "Productos", "precio"))
        {
            connection.Execute(@"UPDATE dbo.Productos
                                 SET precioVenta = CASE
                                     WHEN precioVenta IS NULL OR precioVenta = 0 THEN ISNULL(precio, 0)
                                     ELSE precioVenta
                                 END;");
        }

        var columnasStock = new[]
        {
            ("productoID", "ALTER TABLE dbo.Stock ADD productoID int NULL;"),
            ("cantidad", "ALTER TABLE dbo.Stock ADD cantidad int NOT NULL DEFAULT 0;"),
            ("cantidadMinimoAlerta", "ALTER TABLE dbo.Stock ADD cantidadMinimoAlerta int NOT NULL DEFAULT 0;"),
            ("activo", "ALTER TABLE dbo.Stock ADD activo bit NOT NULL DEFAULT 1;"),
            ("actualizadoEn", "ALTER TABLE dbo.Stock ADD actualizadoEn datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME();")
        };

        foreach (var (columna, sql) in columnasStock)
        {
            if (!ExisteColumna(connection, "Stock", columna))
            {
                connection.Execute(sql);
            }
        }

        if (ExisteColumna(connection, "Productos", "stockActual"))
        {
            var minimo = ExisteColumna(connection, "Productos", "stockMinimo")
                ? "ISNULL(p.stockMinimo, 0)"
                : "0";

            connection.Execute($@"INSERT INTO dbo.Stock
                                      (productoID, cantidad, cantidadMinimoAlerta, activo, actualizadoEn)
                                  SELECT p.id, ISNULL(p.stockActual, 0), {minimo}, 1, SYSUTCDATETIME()
                                  FROM dbo.Productos p
                                  WHERE NOT EXISTS (
                                      SELECT 1 FROM dbo.Stock s WHERE s.productoID = p.id);");
        }

        if (ExisteColumna(connection, "Productos", "categoria"))
        {
            connection.Execute(@"INSERT INTO dbo.Categoria (empresaID, nombre)
                                 SELECT DISTINCT p.empresaID, LTRIM(RTRIM(p.categoria))
                                 FROM dbo.Productos p
                                 WHERE p.categoria IS NOT NULL
                                   AND LTRIM(RTRIM(p.categoria)) <> ''
                                   AND NOT EXISTS (
                                       SELECT 1
                                       FROM dbo.Categoria c
                                       WHERE c.empresaID = p.empresaID
                                         AND LOWER(LTRIM(RTRIM(c.nombre))) = LOWER(LTRIM(RTRIM(p.categoria))));");

            connection.Execute(@"UPDATE p
                                 SET categoriaID = c.id
                                 FROM dbo.Productos p
                                 INNER JOIN dbo.Categoria c
                                     ON c.empresaID = p.empresaID
                                    AND LOWER(LTRIM(RTRIM(c.nombre))) = LOWER(LTRIM(RTRIM(p.categoria)))
                                 WHERE p.categoriaID IS NULL OR p.categoriaID = 0;");
        }

        if (ExisteColumna(connection, "Productos", "proveedor"))
        {
            connection.Execute(@"INSERT INTO dbo.Proveedores (empresaID, nombre)
                                 SELECT DISTINCT p.empresaID, LTRIM(RTRIM(p.proveedor))
                                 FROM dbo.Productos p
                                 WHERE p.proveedor IS NOT NULL
                                   AND LTRIM(RTRIM(p.proveedor)) <> ''
                                   AND NOT EXISTS (
                                       SELECT 1
                                       FROM dbo.Proveedores pe
                                       WHERE pe.empresaID = p.empresaID
                                         AND LOWER(LTRIM(RTRIM(pe.nombre))) = LOWER(LTRIM(RTRIM(p.proveedor))));");

            connection.Execute(@"UPDATE p
                                 SET proveedorID = pe.id
                                 FROM dbo.Productos p
                                 INNER JOIN dbo.Proveedores pe
                                     ON pe.empresaID = p.empresaID
                                    AND LOWER(LTRIM(RTRIM(pe.nombre))) = LOWER(LTRIM(RTRIM(p.proveedor)))
                                 WHERE p.proveedorID IS NULL OR p.proveedorID = 0;");
        }

        var columnasVentas = new[]
        {
            ("empresaID", "ALTER TABLE dbo.Ventas ADD empresaID int NULL;"),
            ("usuarioID", "ALTER TABLE dbo.Ventas ADD usuarioID int NULL;"),
            ("fecha", "ALTER TABLE dbo.Ventas ADD fecha datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME();"),
            ("metodoPago", "ALTER TABLE dbo.Ventas ADD metodoPago varchar(50) NULL;"),
            ("totalVenta", "ALTER TABLE dbo.Ventas ADD totalVenta decimal(12,2) NOT NULL DEFAULT 0;"),
            ("observacion", "ALTER TABLE dbo.Ventas ADD observacion varchar(500) NULL;")
        };

        foreach (var (columna, sql) in columnasVentas)
        {
            if (!ExisteColumna(connection, "Ventas", columna))
            {
                connection.Execute(sql);
            }
        }

        if (ExisteColumna(connection, "Ventas", "precio"))
        {
            connection.Execute(@"UPDATE dbo.Ventas
                                 SET totalVenta = ISNULL(precio, 0)
                                 WHERE totalVenta = 0;");
        }

        var columnasDetalleVenta = new[]
        {
            ("ventaID", "ALTER TABLE dbo.DetalleVenta ADD ventaID int NULL;"),
            ("productoID", "ALTER TABLE dbo.DetalleVenta ADD productoID int NULL;"),
            ("cantidad", "ALTER TABLE dbo.DetalleVenta ADD cantidad int NOT NULL DEFAULT 1;"),
            ("precioVenta", "ALTER TABLE dbo.DetalleVenta ADD precioVenta decimal(12,2) NOT NULL DEFAULT 0;"),
            ("costoCompra", "ALTER TABLE dbo.DetalleVenta ADD costoCompra decimal(12,2) NOT NULL DEFAULT 0;"),
            ("porcentajeGanancia", "ALTER TABLE dbo.DetalleVenta ADD porcentajeGanancia decimal(10,2) NULL;"),
            ("ganancia", "ALTER TABLE dbo.DetalleVenta ADD ganancia decimal(12,2) NULL;")
        };

        foreach (var (columna, sql) in columnasDetalleVenta)
        {
            if (!ExisteColumna(connection, "DetalleVenta", columna))
            {
                connection.Execute(sql);
            }
        }

        if (ExisteColumna(connection, "DetalleVenta", "precioCompra"))
        {
            connection.Execute(@"UPDATE dbo.DetalleVenta
                                 SET costoCompra = ISNULL(precioCompra, 0)
                                 WHERE costoCompra = 0;");
        }

        connection.Execute(@"
            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = 'UQ_DetalleVenta_Venta_Producto'
                  AND object_id = OBJECT_ID('dbo.DetalleVenta')
            )
            BEGIN
                DELETE d
                FROM dbo.DetalleVenta d
                WHERE d.ventaID IS NULL
                   OR d.productoID IS NULL;

                IF OBJECT_ID('tempdb..#DuplicadosDetalleVenta') IS NOT NULL
                    DROP TABLE #DuplicadosDetalleVenta;

                SELECT
                    MIN(id) AS idConservar,
                    ventaID,
                    productoID,
                    SUM(ISNULL(cantidad, 0)) AS cantidadTotal,
                    SUM(ISNULL(precioVenta, 0)) AS precioVentaTotal,
                    SUM(ISNULL(costoCompra, 0)) AS costoCompraTotal,
                    SUM(ISNULL(ganancia, 0)) AS gananciaTotal,
                    COUNT(*) AS cantidadFilas
                INTO #DuplicadosDetalleVenta
                FROM dbo.DetalleVenta
                GROUP BY ventaID, productoID
                HAVING COUNT(*) > 1;

                UPDATE d
                SET d.cantidad = x.cantidadTotal,
                    d.precioVenta = x.precioVentaTotal,
                    d.costoCompra = x.costoCompraTotal,
                    d.ganancia = x.gananciaTotal,
                    d.porcentajeGanancia = CASE
                        WHEN x.precioVentaTotal = 0 THEN NULL
                        ELSE ROUND((x.gananciaTotal / x.precioVentaTotal) * 100, 2)
                    END
                FROM dbo.DetalleVenta d
                INNER JOIN #DuplicadosDetalleVenta x ON x.idConservar = d.id;

                DELETE d
                FROM dbo.DetalleVenta d
                INNER JOIN #DuplicadosDetalleVenta x
                    ON x.ventaID = d.ventaID
                   AND x.productoID = d.productoID
                WHERE d.id <> x.idConservar;

                DROP TABLE #DuplicadosDetalleVenta;

                CREATE UNIQUE NONCLUSTERED INDEX UQ_DetalleVenta_Venta_Producto
                    ON dbo.DetalleVenta (ventaID, productoID);
            END;
        ");

        connection.Execute(@"
            IF OBJECT_ID(N'dbo.IAChats', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.IAChats (
                    Id uniqueidentifier NOT NULL CONSTRAINT PK_IAChats PRIMARY KEY,
                    UsuarioId int NOT NULL,
                    EmpresaId int NOT NULL,
                    Titulo nvarchar(120) NOT NULL,
                    CreadoEn datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    ActualizadoEn datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
                );
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = 'IX_IAChats_Usuario'
                  AND object_id = OBJECT_ID('dbo.IAChats')
            )
            BEGIN
                CREATE INDEX IX_IAChats_Usuario
                    ON dbo.IAChats(UsuarioId, EmpresaId, ActualizadoEn DESC);
            END;

            IF OBJECT_ID(N'dbo.IAMensajes', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.IAMensajes (
                    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_IAMensajes PRIMARY KEY,
                    ChatId uniqueidentifier NOT NULL,
                    Rol varchar(16) NOT NULL,
                    Texto nvarchar(4000) NOT NULL,
                    ContenidoJson nvarchar(max) NULL,
                    CreadoEn datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_IAMensajes_IAChats
                        FOREIGN KEY(ChatId) REFERENCES dbo.IAChats(Id) ON DELETE CASCADE,
                    CONSTRAINT CK_IAMensajes_Rol
                        CHECK (Rol IN ('user', 'assistant'))
                );
            END;

            IF COL_LENGTH(N'dbo.IAMensajes', N'ContenidoJson') IS NULL
            BEGIN
                ALTER TABLE dbo.IAMensajes
                    ADD ContenidoJson nvarchar(max) NULL;
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = 'IX_IAMensajes_Chat'
                  AND object_id = OBJECT_ID('dbo.IAMensajes')
            )
            BEGIN
                CREATE INDEX IX_IAMensajes_Chat
                    ON dbo.IAMensajes(ChatId, Id);
            END;

            IF OBJECT_ID(N'dbo.IAAuditoria', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.IAAuditoria (
                    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_IAAuditoria PRIMARY KEY,
                    UsuarioId int NOT NULL,
                    EmpresaId int NOT NULL,
                    Accion varchar(30) NOT NULL,
                    ProductoId int NULL,
                    Antes nvarchar(1000) NULL,
                    Despues nvarchar(1000) NULL,
                    Fecha datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
                );
            END;
        ");

        connection.Execute(@"
            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = 'IX_Ventas_Fecha_Id'
                  AND object_id = OBJECT_ID('dbo.Ventas')
            )
            BEGIN
                CREATE NONCLUSTERED INDEX IX_Ventas_Fecha_Id
                    ON dbo.Ventas (fecha, id)
                    INCLUDE (metodoPago);
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = 'IX_DetalleVenta_VentaId_ProductoId'
                  AND object_id = OBJECT_ID('dbo.DetalleVenta')
            )
            BEGIN
                CREATE NONCLUSTERED INDEX IX_DetalleVenta_VentaId_ProductoId
                    ON dbo.DetalleVenta (ventaID, productoID)
                    INCLUDE (cantidad, precioVenta, costoCompra);
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = 'IX_Productos_Empresa_Id'
                  AND object_id = OBJECT_ID('dbo.Productos')
            )
            BEGIN
                CREATE NONCLUSTERED INDEX IX_Productos_Empresa_Id
                    ON dbo.Productos (empresaID, id)
                    INCLUDE (nombre, sku, categoriaID);
            END;
        ");
    }

}
