using Dapper;
using Quantitary.Models;

namespace Quantitary.Services;

/// <summary>
/// Inicializa la cuenta de exposición desde recursos incluidos en la aplicación.
/// No requiere que el usuario ejecute manualmente el bloque de datos demo del SQL.
/// </summary>
public sealed class DatosDemoService
{
    public const string Mail = "demo@quantitary.com";
    public const string Contrasena = "Expo2026";

    private const string NombreEmpresa = "Almacén Horizonte Demo";
    private const string NombreUsuario = "Usuario Demo";
    private const string NombreRol = "Administrador Expo";
    private const string RecursoExcel = "Quantitary.DatosDemo.Productos_Demo_Expo.xlsx";

    private readonly IConfiguration _configuration;

    public DatosDemoService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void GarantizarCuentaYDatos()
    {
        var productosExcel = LeerCatalogoIntegrado();

        using var connection = BD.CrearConexion(_configuration);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        GarantizarCompatibilidad(connection, transaction);

        var empresaId = connection.QuerySingleOrDefault<int?>(@"
            SELECT TOP 1 id
            FROM Empresa
            WHERE nombre = @Nombre
            ORDER BY id;",
            new { Nombre = NombreEmpresa }, transaction);

        if (!empresaId.HasValue)
        {
            empresaId = connection.QuerySingle<int>(@"
                INSERT INTO Empresa (nombre)
                VALUES (@Nombre);
                SELECT CAST(SCOPE_IDENTITY() AS int);",
                new { Nombre = NombreEmpresa }, transaction);
        }

        var permisosId = connection.QuerySingleOrDefault<int?>(@"
            SELECT TOP 1 id
            FROM Permisos
            WHERE registrarVentas = 1
              AND verHistorialVentas = 1
              AND anularVentas = 1
              AND verProductos = 1
              AND crearProductos = 1
              AND editarProductos = 1
              AND eliminarProductos = 1
              AND verStock = 1
              AND verDashboard = 1
              AND gestionarUsuarios = 1
              AND verGanacia = 1
            ORDER BY id;", transaction: transaction);

        if (!permisosId.HasValue)
        {
            permisosId = connection.QuerySingle<int>(@"
                INSERT INTO Permisos
                    (registrarVentas, verHistorialVentas, anularVentas, verProductos,
                     crearProductos, editarProductos, eliminarProductos, verStock,
                     verDashboard, gestionarUsuarios, verGanacia)
                VALUES (1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
                SELECT CAST(SCOPE_IDENTITY() AS int);", transaction: transaction);
        }

        var rolId = connection.QuerySingleOrDefault<int?>(@"
            SELECT TOP 1 id
            FROM Rol
            WHERE empresaID = @EmpresaId
              AND nombre = @Nombre
            ORDER BY id;",
            new { EmpresaId = empresaId.Value, Nombre = NombreRol }, transaction);

        if (!rolId.HasValue)
        {
            rolId = connection.QuerySingle<int>(@"
                INSERT INTO Rol (empresaID, nombre, permisoID)
                VALUES (@EmpresaId, @Nombre, @PermisoId);
                SELECT CAST(SCOPE_IDENTITY() AS int);",
                new { EmpresaId = empresaId.Value, Nombre = NombreRol, PermisoId = permisosId.Value }, transaction);
        }
        else
        {
            connection.Execute(@"
                UPDATE Rol
                SET permisoID = @PermisoId
                WHERE id = @RolId;",
                new { PermisoId = permisosId.Value, RolId = rolId.Value }, transaction);
        }

        var usuarioId = connection.QuerySingleOrDefault<int?>(@"
            SELECT TOP 1 id
            FROM Usuarios
            WHERE LOWER(LTRIM(RTRIM(mail))) = LOWER(@Mail)
            ORDER BY id;",
            new { Mail }, transaction);

        if (!usuarioId.HasValue)
        {
            usuarioId = connection.QuerySingle<int>(@"
                INSERT INTO Usuarios
                    (nombreUsuario, contrasena, mail, numeroTelefono, empresaID, rolID)
                VALUES
                    (@NombreUsuario, @Contrasena, @Mail, NULL, @EmpresaId, @RolId);
                SELECT CAST(SCOPE_IDENTITY() AS int);",
                new
                {
                    NombreUsuario,
                    Contrasena,
                    Mail,
                    EmpresaId = empresaId.Value,
                    RolId = rolId.Value
                }, transaction);
        }
        else
        {
            connection.Execute(@"
                UPDATE Usuarios
                SET nombreUsuario = @NombreUsuario,
                    contrasena = @Contrasena,
                    empresaID = @EmpresaId,
                    rolID = @RolId
                WHERE id = @UsuarioId;",
                new
                {
                    NombreUsuario,
                    Contrasena,
                    EmpresaId = empresaId.Value,
                    RolId = rolId.Value,
                    UsuarioId = usuarioId.Value
                }, transaction);
        }

        var productosPorSku = new Dictionary<string, ProductoDemoDb>(StringComparer.OrdinalIgnoreCase);

        foreach (var producto in productosExcel)
        {
            var categoriaId = ObtenerOCrearCategoria(connection, transaction, empresaId.Value, producto.Categoria);
            var proveedorId = ObtenerOCrearProveedor(connection, transaction, empresaId.Value, producto.Proveedor);

            var productoIdExistente = connection.QuerySingleOrDefault<int?>(@"
                SELECT TOP 1 id
                FROM Productos
                WHERE empresaID = @EmpresaId
                  AND LOWER(LTRIM(RTRIM(sku))) = LOWER(LTRIM(RTRIM(@Sku)))
                ORDER BY id;",
                new { EmpresaId = empresaId.Value, producto.Sku }, transaction);

            ProductoDemoDb productoDb;

            if (!productoIdExistente.HasValue)
            {
                var productoId = connection.QuerySingle<int>(@"
                    INSERT INTO Productos
                        (empresaID, categoriaID, proveedorID, nombre, sku, descripcion,
                         fechaCarga, precioVenta, costoCompra, activo)
                    VALUES
                        (@EmpresaId, @CategoriaId, @ProveedorId, @Nombre, @Sku,
                         'Producto del catálogo Excel de demostración', SYSUTCDATETIME(),
                         @PrecioVenta, @CostoCompra, 1);
                    SELECT CAST(SCOPE_IDENTITY() AS int);",
                    new
                    {
                        EmpresaId = empresaId.Value,
                        CategoriaId = categoriaId,
                        ProveedorId = proveedorId,
                        producto.Nombre,
                        producto.Sku,
                        producto.PrecioVenta,
                        producto.CostoCompra
                    }, transaction);

                productoDb = new ProductoDemoDb(productoId, producto.PrecioVenta, producto.CostoCompra);
            }
            else
            {
                connection.Execute(@"
                    UPDATE Productos
                    SET categoriaID = @CategoriaId,
                        proveedorID = @ProveedorId,
                        nombre = @Nombre,
                        descripcion = 'Producto del catálogo Excel de demostración',
                        precioVenta = @PrecioVenta,
                        costoCompra = @CostoCompra,
                        activo = 1
                    WHERE id = @ProductoId
                      AND empresaID = @EmpresaId;",
                    new
                    {
                        CategoriaId = categoriaId,
                        ProveedorId = proveedorId,
                        producto.Nombre,
                        producto.PrecioVenta,
                        producto.CostoCompra,
                        ProductoId = productoIdExistente.Value,
                        EmpresaId = empresaId.Value
                    }, transaction);

                productoDb = new ProductoDemoDb(
                    productoIdExistente.Value,
                    producto.PrecioVenta,
                    producto.CostoCompra);
            }

            var stockId = connection.QuerySingleOrDefault<int?>(@"
                SELECT TOP 1 id
                FROM Stock
                WHERE productoID = @ProductoId
                ORDER BY id;",
                new { ProductoId = productoDb.Id }, transaction);

            if (stockId.HasValue)
            {
                connection.Execute(@"
                    UPDATE Stock
                    SET cantidad = @Cantidad,
                        cantidadMinimoAlerta = @Minimo,
                        activo = 1,
                        actualizadoEn = SYSUTCDATETIME()
                    WHERE id = @StockId;",
                    new
                    {
                        Cantidad = Math.Max(0, producto.StockActual),
                        Minimo = Math.Max(0, producto.StockMinimo),
                        StockId = stockId.Value
                    }, transaction);
            }
            else
            {
                connection.Execute(@"
                    INSERT INTO Stock
                        (productoID, cantidad, cantidadMinimoAlerta, activo, actualizadoEn)
                    VALUES
                        (@ProductoId, @Cantidad, @Minimo, 1, SYSUTCDATETIME());",
                    new
                    {
                        ProductoId = productoDb.Id,
                        Cantidad = Math.Max(0, producto.StockActual),
                        Minimo = Math.Max(0, producto.StockMinimo)
                    }, transaction);
            }

            productosPorSku[producto.Sku] = productoDb;
        }

        GarantizarVentas(connection, transaction, empresaId.Value, usuarioId.Value, productosPorSku);
        transaction.Commit();
    }

    private static List<Producto> LeerCatalogoIntegrado()
    {
        using var stream = typeof(DatosDemoService).Assembly.GetManifestResourceStream(RecursoExcel)
            ?? throw new InvalidOperationException("No se encontró el catálogo Excel integrado de la cuenta demo.");

        return ImportacionExcelHelper.LeerProductos(stream);
    }

    private static void GarantizarCompatibilidad(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction)
    {
        connection.Execute(@"
            IF COL_LENGTH(N'dbo.Rol', N'empresaID') IS NULL
                ALTER TABLE dbo.Rol ADD empresaID int NULL;

            DECLARE @Columna sysname;
            DECLARE @Tipo nvarchar(128);
            DECLARE @Sql nvarchar(max);

            DECLARE cursor_productos_demo CURSOR LOCAL FAST_FORWARD FOR
            SELECT
                c.[name],
                CASE
                    WHEN t.[name] IN ('varchar', 'char', 'varbinary', 'binary')
                        THEN t.[name] + '(' + CASE WHEN c.[max_length] = -1 THEN 'max' ELSE CONVERT(varchar(10), c.[max_length]) END + ')'
                    WHEN t.[name] IN ('nvarchar', 'nchar')
                        THEN t.[name] + '(' + CASE WHEN c.[max_length] = -1 THEN 'max' ELSE CONVERT(varchar(10), c.[max_length] / 2) END + ')'
                    WHEN t.[name] IN ('decimal', 'numeric')
                        THEN t.[name] + '(' + CONVERT(varchar(10), c.[precision]) + ',' + CONVERT(varchar(10), c.[scale]) + ')'
                    WHEN t.[name] IN ('datetime2', 'datetimeoffset', 'time')
                        THEN t.[name] + '(' + CONVERT(varchar(10), c.[scale]) + ')'
                    ELSE t.[name]
                END
            FROM sys.columns c
            INNER JOIN sys.types t ON t.[user_type_id] = c.[user_type_id]
            WHERE c.[object_id] = OBJECT_ID(N'dbo.Productos')
              AND c.[name] IN (
                  'nombreProducto', 'categoria', 'proveedor', 'marca', 'precio',
                  'stockActual', 'stockMinimo', 'peso', 'unidadMedida', 'cantidadVentas'
              )
              AND c.[is_nullable] = 0
              AND c.[is_identity] = 0
              AND c.[is_computed] = 0;

            OPEN cursor_productos_demo;
            FETCH NEXT FROM cursor_productos_demo INTO @Columna, @Tipo;
            WHILE @@FETCH_STATUS = 0
            BEGIN
                SET @Sql = N'ALTER TABLE dbo.Productos ALTER COLUMN ' + QUOTENAME(@Columna) + N' ' + @Tipo + N' NULL;';
                EXEC sys.sp_executesql @Sql;
                FETCH NEXT FROM cursor_productos_demo INTO @Columna, @Tipo;
            END;
            CLOSE cursor_productos_demo;
            DEALLOCATE cursor_productos_demo;", transaction: transaction);

        HacerColumnaLegacyNullable(connection, transaction, "Proveedores", "productoPrincipal");
    }

    private static bool ExisteColumna(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        string tabla,
        string columna)
    {
        return connection.QuerySingle<int>(@"
            SELECT CASE WHEN COL_LENGTH(@Tabla, @Columna) IS NULL THEN 0 ELSE 1 END;",
            new { Tabla = $"dbo.{tabla}", Columna = columna }, transaction) == 1;
    }

    private static void HacerColumnaLegacyNullable(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        string tabla,
        string columna)
    {
        connection.Execute(@"
            DECLARE @TablaCompleta nvarchar(260) = N'dbo.' + QUOTENAME(@Tabla);
            DECLARE @Objeto int = OBJECT_ID(@TablaCompleta);
            DECLARE @Tipo nvarchar(128);

            SELECT @Tipo =
                CASE
                    WHEN t.[name] IN ('varchar', 'char', 'varbinary', 'binary')
                        THEN t.[name] + '(' + CASE WHEN c.[max_length] = -1 THEN 'max' ELSE CONVERT(varchar(10), c.[max_length]) END + ')'
                    WHEN t.[name] IN ('nvarchar', 'nchar')
                        THEN t.[name] + '(' + CASE WHEN c.[max_length] = -1 THEN 'max' ELSE CONVERT(varchar(10), c.[max_length] / 2) END + ')'
                    WHEN t.[name] IN ('decimal', 'numeric')
                        THEN t.[name] + '(' + CONVERT(varchar(10), c.[precision]) + ',' + CONVERT(varchar(10), c.[scale]) + ')'
                    WHEN t.[name] IN ('datetime2', 'datetimeoffset', 'time')
                        THEN t.[name] + '(' + CONVERT(varchar(10), c.[scale]) + ')'
                    ELSE t.[name]
                END
            FROM sys.columns c
            INNER JOIN sys.types t ON t.[user_type_id] = c.[user_type_id]
            WHERE c.[object_id] = @Objeto
              AND c.[name] = @Columna
              AND c.[is_nullable] = 0
              AND c.[is_identity] = 0
              AND c.[is_computed] = 0;

            IF @Tipo IS NOT NULL
            BEGIN
                DECLARE @Sql nvarchar(max) =
                    N'ALTER TABLE dbo.' + QUOTENAME(@Tabla) +
                    N' ALTER COLUMN ' + QUOTENAME(@Columna) + N' ' + @Tipo + N' NULL;';
                EXEC sys.sp_executesql @Sql;
            END;",
            new { Tabla = tabla, Columna = columna }, transaction);
    }

    private static int ObtenerOCrearCategoria(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, int empresaId, string? nombre)
    {
        var nombreFinal = string.IsNullOrWhiteSpace(nombre) ? "Sin categoría" : nombre.Trim();
        var id = connection.QuerySingleOrDefault<int?>(@"
            SELECT TOP 1 id
            FROM Categoria
            WHERE empresaID = @EmpresaId
              AND LOWER(LTRIM(RTRIM(nombre))) = LOWER(LTRIM(RTRIM(@Nombre)))
            ORDER BY id;",
            new { EmpresaId = empresaId, Nombre = nombreFinal }, transaction);

        return id ?? connection.QuerySingle<int>(@"
            INSERT INTO Categoria (empresaID, nombre)
            VALUES (@EmpresaId, @Nombre);
            SELECT CAST(SCOPE_IDENTITY() AS int);",
            new { EmpresaId = empresaId, Nombre = nombreFinal }, transaction);
    }

    private static int? ObtenerOCrearProveedor(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, int empresaId, string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return null;
        }

        var nombreFinal = nombre.Trim();
        var id = connection.QuerySingleOrDefault<int?>(@"
            SELECT TOP 1 id
            FROM Proveedores
            WHERE empresaID = @EmpresaId
              AND LOWER(LTRIM(RTRIM(nombre))) = LOWER(LTRIM(RTRIM(@Nombre)))
            ORDER BY id;",
            new { EmpresaId = empresaId, Nombre = nombreFinal }, transaction);

        return id ?? connection.QuerySingle<int>(@"
            INSERT INTO Proveedores
                (empresaID, nombre, telefono, mail, costoEnvio, detalles)
            VALUES
                (@EmpresaId, @Nombre, NULL, NULL, 0, 'Proveedor del catálogo demo');
            SELECT CAST(SCOPE_IDENTITY() AS int);",
            new { EmpresaId = empresaId, Nombre = nombreFinal }, transaction);
    }

    private static void GarantizarVentas(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        int empresaId,
        int usuarioId,
        IReadOnlyDictionary<string, ProductoDemoDb> productos)
    {
        var ahora = DateTime.Now;
        var ventas = new[]
        {
            new VentaDemo("EXPO-DEMO-001", ahora.AddMinutes(-15), "BEB-COCA-200", 2, "Tarjeta"),
            new VentaDemo("EXPO-DEMO-002", ahora.AddMinutes(-35), "BEB-AGUA-150", 3, "Efectivo"),
            new VentaDemo("EXPO-DEMO-003", ahora.AddMinutes(-55), "ALM-YERBA-100", 1, "Transferencia"),
            new VentaDemo("EXPO-DEMO-004", ahora.AddHours(-2), "SNK-GALLE-030", 2, "Tarjeta"),
            new VentaDemo("EXPO-DEMO-005", ahora.AddHours(-3), "LAC-LECHE-100", 4, "Efectivo"),
            new VentaDemo("EXPO-DEMO-006", ahora.AddDays(-1), "BEB-COCA-225", 3, "Efectivo"),
            new VentaDemo("EXPO-DEMO-007", ahora.AddDays(-1).AddHours(-2), "ALM-ARROZ-100", 5, "Transferencia"),
            new VentaDemo("EXPO-DEMO-008", ahora.AddDays(-2), "LAC-YOGUR-019", 4, "Efectivo"),
            new VentaDemo("EXPO-DEMO-009", ahora.AddDays(-3), "BEB-COCA-200", 4, "Tarjeta"),
            new VentaDemo("EXPO-DEMO-010", ahora.AddDays(-4), "ALM-AZUCAR-100", 2, "Efectivo"),
            new VentaDemo("EXPO-DEMO-011", ahora.AddDays(-7), "ALM-ACEITE-150", 2, "Tarjeta"),
            new VentaDemo("EXPO-DEMO-012", ahora.AddDays(-10), "SNK-CHOCO-010", 3, "Efectivo"),
            new VentaDemo("EXPO-DEMO-013", ahora.AddDays(-14), "LAC-LECHE-100", 6, "Efectivo"),
            new VentaDemo("EXPO-DEMO-014", ahora.AddDays(-21), "LIM-PAPEL-004", 1, "Tarjeta"),
            new VentaDemo("EXPO-DEMO-015", ahora.AddDays(-25), "BEB-AGUA-150", 4, "Efectivo"),
            new VentaDemo("EXPO-DEMO-016", ahora.AddMonths(-1), "BEB-COCA-200", 5, "Tarjeta"),
            new VentaDemo("EXPO-DEMO-017", ahora.AddMonths(-1).AddDays(-8), "ALM-ARROZ-100", 4, "Efectivo"),
            new VentaDemo("EXPO-DEMO-018", ahora.AddMonths(-2), "BEB-COCA-225", 6, "Efectivo"),
            new VentaDemo("EXPO-DEMO-019", ahora.AddMonths(-2).AddDays(-5), "LAC-LECHE-100", 8, "Tarjeta"),
            new VentaDemo("EXPO-DEMO-020", ahora.AddMonths(-3), "BEB-COCA-200", 7, "Tarjeta"),
            new VentaDemo("EXPO-DEMO-021", ahora.AddMonths(-3).AddDays(-8), "BEB-AGUA-150", 6, "Efectivo"),
            new VentaDemo("EXPO-DEMO-022", ahora.AddMonths(-5), "BEB-JUGO-100", 5, "Tarjeta"),
            new VentaDemo("EXPO-DEMO-023", ahora.AddMonths(-6), "LIM-PAPEL-004", 1, "Transferencia"),
            new VentaDemo("EXPO-DEMO-024", ahora.AddMonths(-6).AddDays(-12), "SNK-CHOCO-010", 4, "Tarjeta")
        };
        var usaVentaLegacy = ExisteColumna(connection, transaction, "Ventas", "productoId");
        var usaDetalleLegacy = ExisteColumna(connection, transaction, "DetalleVenta", "precioCompra");

        foreach (var venta in ventas)
        {
            if (!productos.TryGetValue(venta.Sku, out var producto))
            {
                continue;
            }

            var totalVenta = producto.PrecioVenta * venta.Cantidad;
            var costoCompra = producto.CostoCompra * venta.Cantidad;
            var ganancia = totalVenta - costoCompra;
            var porcentaje = producto.CostoCompra <= 0
                ? (decimal?)null
                : Math.Round(((producto.PrecioVenta - producto.CostoCompra) / producto.CostoCompra) * 100m, 2);

            var ventaId = connection.QuerySingleOrDefault<int?>(@"
                SELECT TOP 1 id
                FROM Ventas
                WHERE empresaID = @EmpresaId
                  AND observacion = @Codigo
                ORDER BY id;",
                new { EmpresaId = empresaId, venta.Codigo }, transaction);

            if (!ventaId.HasValue)
            {
                var parametrosVenta = new
                {
                    EmpresaId = empresaId,
                    UsuarioId = usuarioId,
                    ProductoId = producto.Id,
                    NombreProducto = venta.Sku,
                    venta.Fecha,
                    venta.MetodoPago,
                    venta.Cantidad,
                    Precio = totalVenta,
                    Costo = costoCompra,
                    TotalVenta = totalVenta,
                    venta.Codigo
                };

                ventaId = usaVentaLegacy
                    ? connection.QuerySingle<int>(@"
                        INSERT INTO Ventas
                            (productoId, nombreProducto, empresaID, usuarioID, fecha,
                             metodoPago, cantidad, precio, costo, totalVenta, observacion)
                        VALUES
                            (@ProductoId, @NombreProducto, @EmpresaId, @UsuarioId, @Fecha,
                             @MetodoPago, @Cantidad, @Precio, @Costo, @TotalVenta, @Codigo);
                        SELECT CAST(SCOPE_IDENTITY() AS int);", parametrosVenta, transaction)
                    : connection.QuerySingle<int>(@"
                        INSERT INTO Ventas
                            (empresaID, usuarioID, fecha, metodoPago, totalVenta, observacion)
                        VALUES
                            (@EmpresaId, @UsuarioId, @Fecha, @MetodoPago, @TotalVenta, @Codigo);
                        SELECT CAST(SCOPE_IDENTITY() AS int);", parametrosVenta, transaction);
            }
            else
            {
                var parametrosVenta = new
                {
                    UsuarioId = usuarioId,
                    ProductoId = producto.Id,
                    NombreProducto = venta.Sku,
                    venta.Fecha,
                    venta.MetodoPago,
                    venta.Cantidad,
                    Precio = totalVenta,
                    Costo = costoCompra,
                    TotalVenta = totalVenta,
                    VentaId = ventaId.Value
                };

                if (usaVentaLegacy)
                {
                    connection.Execute(@"
                        UPDATE Ventas
                        SET usuarioID = @UsuarioId,
                            productoId = @ProductoId,
                            nombreProducto = @NombreProducto,
                            fecha = @Fecha,
                            metodoPago = @MetodoPago,
                            cantidad = @Cantidad,
                            precio = @Precio,
                            costo = @Costo,
                            totalVenta = @TotalVenta
                        WHERE id = @VentaId;", parametrosVenta, transaction);
                }
                else
                {
                    connection.Execute(@"
                        UPDATE Ventas
                        SET usuarioID = @UsuarioId,
                            fecha = @Fecha,
                            metodoPago = @MetodoPago,
                            totalVenta = @TotalVenta
                        WHERE id = @VentaId;", parametrosVenta, transaction);
                }
            }

            var detalleId = connection.QuerySingleOrDefault<int?>(@"
                SELECT TOP 1 id
                FROM DetalleVenta
                WHERE ventaID = @VentaId
                  AND productoID = @ProductoId
                ORDER BY id;",
                new { VentaId = ventaId.Value, ProductoId = producto.Id }, transaction);

            if (detalleId.HasValue)
            {
                var parametrosDetalle = new
                {
                    venta.Cantidad,
                    PrecioVenta = totalVenta,
                    CostoCompra = costoCompra,
                    Porcentaje = porcentaje,
                    Ganancia = ganancia,
                    DetalleId = detalleId.Value
                };

                if (usaDetalleLegacy)
                {
                    connection.Execute(@"
                        UPDATE DetalleVenta
                        SET cantidad = @Cantidad,
                            precioVenta = @PrecioVenta,
                            precioCompra = @CostoCompra,
                            costoCompra = @CostoCompra,
                            porcentajeGanancia = @Porcentaje,
                            ganancia = @Ganancia
                        WHERE id = @DetalleId;", parametrosDetalle, transaction);
                }
                else
                {
                    connection.Execute(@"
                        UPDATE DetalleVenta
                        SET cantidad = @Cantidad,
                            precioVenta = @PrecioVenta,
                            costoCompra = @CostoCompra,
                            porcentajeGanancia = @Porcentaje,
                            ganancia = @Ganancia
                        WHERE id = @DetalleId;", parametrosDetalle, transaction);
                }
            }
            else
            {
                var parametrosDetalle = new
                {
                    VentaId = ventaId.Value,
                    ProductoId = producto.Id,
                    venta.Cantidad,
                    PrecioVenta = totalVenta,
                    CostoCompra = costoCompra,
                    Porcentaje = porcentaje,
                    Ganancia = ganancia
                };

                if (usaDetalleLegacy)
                {
                    connection.Execute(@"
                        INSERT INTO DetalleVenta
                            (ventaID, productoID, cantidad, precioVenta, precioCompra,
                             costoCompra, porcentajeGanancia, ganancia)
                        VALUES
                            (@VentaId, @ProductoId, @Cantidad, @PrecioVenta, @CostoCompra,
                             @CostoCompra, @Porcentaje, @Ganancia);",
                        parametrosDetalle, transaction);
                }
                else
                {
                    connection.Execute(@"
                        INSERT INTO DetalleVenta
                            (ventaID, productoID, cantidad, precioVenta, costoCompra,
                             porcentajeGanancia, ganancia)
                        VALUES
                            (@VentaId, @ProductoId, @Cantidad, @PrecioVenta, @CostoCompra,
                             @Porcentaje, @Ganancia);",
                        parametrosDetalle, transaction);
                }
            }
        }
    }

    private sealed record ProductoDemoDb(int Id, decimal PrecioVenta, decimal CostoCompra);
    private sealed record VentaDemo(string Codigo, DateTime Fecha, string Sku, int Cantidad, string MetodoPago);
}
