using System.Data;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

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

    public static List<string> ObtenerCategorias(IConfiguration configuration, int? empresaId = null)
    {
        using var connection = CrearConexion(configuration);
        var sql = @"SELECT DISTINCT LTRIM(RTRIM(categoria)) AS categoria
                    FROM Productos
                    WHERE (@EmpresaId IS NULL OR empresaID = @EmpresaId)
                      AND LTRIM(RTRIM(categoria)) <> ''
                    ORDER BY LTRIM(RTRIM(categoria))";
        return connection.Query<string>(sql, new { EmpresaId = empresaId }).ToList();
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
        return connection.Query<Producto>(
            "sp_ObtenerProductosStock",
            parametros,
            commandType: CommandType.StoredProcedure).ToList();
    }

    public static List<Producto> ObtenerProductos(IConfiguration configuration, string? sku = null, string? categoria = null, int? empresaId = null)
        => ObtenerProductos(configuration, sku, categoria, null, null, null, empresaId);

    public static Producto? BuscarPorId(IConfiguration configuration, int id)
    {
        using var connection = CrearConexion(configuration);
        return connection.QuerySingleOrDefault<Producto>("SELECT * FROM Productos WHERE id = @Id", new { Id = id });
    }

    public static Producto AgregarProducto(IConfiguration configuration, Producto producto)
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

        using var connection = CrearConexion(configuration);

        var sql = @"INSERT INTO Productos (empresaID, nombre, sku, categoria, descripcion, fechaCarga, precio, stockActual, stockMinimo, proveedor, costoCompra, precioVenta, cantidadVentas)
                    VALUES (@EmpresaID, @Nombre, @Sku, @Categoria, @Descripcion, @FechaCarga, @Precio, @StockActual, @StockMinimo, @Proveedor, @CostoCompra, @PrecioVenta, @CantidadVentas);
                    SELECT CAST(SCOPE_IDENTITY() as int);";

        producto.Id = connection.QuerySingle<int>(sql, producto);
        return producto;
    }

    public static bool ActualizarProducto(IConfiguration configuration, int id, Producto producto)
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

        using var connection = CrearConexion(configuration);
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

    public static bool EliminarProducto(IConfiguration configuration, int id)
    {
        using var connection = CrearConexion(configuration);
        return connection.Execute("DELETE FROM Productos WHERE id = @Id", new { Id = id }) > 0;
    }

    public static bool AgregarStock(IConfiguration configuration, int id, int cantidad, decimal precioUnitario)
    {
        var sql = @"UPDATE Productos
                    SET stockActual = stockActual + @Cantidad,
                        costoCompra = @PrecioUnitario
                    WHERE id = @Id";

        using var connection = CrearConexion(configuration);
        return connection.Execute(sql, new { Id = id, Cantidad = cantidad, PrecioUnitario = precioUnitario }) > 0;
    }

    public static DashboardSummary ObtenerDashboard(IConfiguration configuration, string range, int? empresaId = null)
    {
        var ventas = ObtenerVentasPorRango(configuration, range, empresaId);
        var mesActual = DateTime.Now;
        var ventasMes = ObtenerVentasPorMes(configuration, empresaId, mesActual);
        var ventasPrevias = ObtenerVentasPorMes(configuration, empresaId, mesActual.AddMonths(-1));

        var resumen = new DashboardSummary
        {
            TotalProductos = ObtenerTotalProductos(configuration, empresaId),
            ProductosPocoStock = ObtenerProductosConStockBajo(configuration, empresaId),
            ProductosSinStock = ObtenerProductosSinStock(configuration, empresaId),
            GananciasPorMesNetas = ventas.Sum(v => v.Precio - v.Costo),
            GananciaPromedioPorProducto = ventas.Any() ? ventas.Average(v => v.Precio - v.Costo) : 0m,
            VentasTotales = ventas.Count,
            VentaPromedioPorMes = ventasMes.Any() ? ventasMes.Average(v => v.Precio) : 0m,
            UltimosProductosVendidos = ventas.OrderByDescending(v => v.Fecha).Take(5).ToList(),
            BasesDeDatos = new List<string> { "Productos", "Ventas", "Proveedores", "Dashboard" },
            TotalVentasMes = ventasMes.Sum(v => v.Precio),
            GananciaNetaMes = ventasMes.Sum(v => v.Precio - v.Costo),
            GastosTotalesMes = ventasMes.Sum(v => v.Costo),
            VentaPromedioMes = ventasMes.Any() ? ventasMes.Average(v => v.Precio) : 0m,
            MesActualLabel = mesActual.ToString("MMMM yyyy"),
            VentasPorPeriodo = ObtenerVentasPorPeriodo(configuration, empresaId, mesActual),
            GananciaPorCategoria = ObtenerGananciaPorCategoria(configuration, empresaId, mesActual),
            ParticipacionVentas = ObtenerParticipacionVentas(configuration, empresaId, mesActual)
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

    private static List<VentaRegistro> ObtenerVentasPorMes(IConfiguration configuration, int? empresaId, DateTime fecha)
    {
        var inicio = new DateTime(fecha.Year, fecha.Month, 1);
        var fin = inicio.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);

        using var connection = CrearConexion(configuration);
        return connection.Query<VentaRegistro>(
            "SELECT * FROM Ventas WHERE productoId IN (SELECT id FROM Productos WHERE (@EmpresaId IS NULL OR empresaID = @EmpresaId)) AND fecha >= @Inicio AND fecha <= @Fin ORDER BY fecha DESC",
            new { EmpresaId = empresaId, Inicio = inicio, Fin = fin }).ToList();
    }

    private static List<ChartPoint> ObtenerVentasPorPeriodo(IConfiguration configuration, int? empresaId, DateTime fecha)
    {
        var inicio = new DateTime(fecha.Year, fecha.Month, 1);
        var labels = new[] { "1-7 sep", "8-14 sep", "15-23 sep", "24-30 sep" };
        var result = new List<ChartPoint>();

        for (int i = 0; i < labels.Length; i++)
        {
            var desde = inicio.AddDays(i * 7);
            var hasta = i == labels.Length - 1 ? inicio.AddMonths(1).AddDays(-1) : desde.AddDays(6);

            using var connection = CrearConexion(configuration);
            var total = connection.QuerySingleOrDefault<decimal?>(
                "SELECT SUM(precio) FROM Ventas WHERE productoId IN (SELECT id FROM Productos WHERE (@EmpresaId IS NULL OR empresaID = @EmpresaId)) AND fecha >= @Desde AND fecha <= @Hasta",
                new { EmpresaId = empresaId, Desde = desde, Hasta = hasta }) ?? 0m;

            result.Add(new ChartPoint { Label = labels[i], Valor = total });
        }

        return result;
    }

    private static List<ChartPoint> ObtenerGananciaPorCategoria(IConfiguration configuration, int? empresaId, DateTime fecha)
    {
        var inicio = new DateTime(fecha.Year, fecha.Month, 1);
        var fin = inicio.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);

        using var connection = CrearConexion(configuration);
        var rows = connection.Query<(string Nombre, decimal Valor)>(
            @"SELECT p.categoria AS Nombre, SUM(v.precio - v.costo) AS Valor
              FROM Ventas v
              INNER JOIN Productos p ON p.id = v.productoId
              WHERE (@EmpresaId IS NULL OR p.empresaID = @EmpresaId)
                AND v.fecha >= @Inicio AND v.fecha <= @Fin
              GROUP BY p.categoria
              ORDER BY Valor DESC",
            new { EmpresaId = empresaId, Inicio = inicio, Fin = fin }).ToList();

        return rows.Select(r => new ChartPoint
        {
            Label = r.Nombre,
            Valor = r.Valor
        }).ToList();
    }

    private static List<DashboardCategoryShare> ObtenerParticipacionVentas(IConfiguration configuration, int? empresaId, DateTime fecha)
    {
        var inicio = new DateTime(fecha.Year, fecha.Month, 1);
        var fin = inicio.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);

        using var connection = CrearConexion(configuration);
        var rows = connection.Query<(string Nombre, decimal Valor)>(
            @"SELECT p.categoria AS Nombre, SUM(v.precio) AS Valor
              FROM Ventas v
              INNER JOIN Productos p ON p.id = v.productoId
              WHERE (@EmpresaId IS NULL OR p.empresaID = @EmpresaId)
                AND v.fecha >= @Inicio AND v.fecha <= @Fin
              GROUP BY p.categoria
              ORDER BY Valor DESC",
            new { EmpresaId = empresaId, Inicio = inicio, Fin = fin }).ToList();

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

    public static bool TieneProductos(IConfiguration configuration, int empresaId)
    {
        using var connection = CrearConexion(configuration);
        return connection.QuerySingle<int>("SELECT COUNT(*) FROM Productos WHERE empresaID = @EmpresaId", new { EmpresaId = empresaId }) > 0;
    }

    private static int ObtenerTotalProductos(IConfiguration configuration, int? empresaId = null)
    {
        using var connection = CrearConexion(configuration);
        var sql = "SELECT COUNT(*) FROM Productos WHERE (@EmpresaId IS NULL OR empresaID = @EmpresaId)";
        return connection.QuerySingle<int>(sql, new { EmpresaId = empresaId });
    }

    private static int ObtenerProductosConStockBajo(IConfiguration configuration, int? empresaId = null)
    {
        using var connection = CrearConexion(configuration);
        var sql = "SELECT COUNT(*) FROM Productos WHERE (@EmpresaId IS NULL OR empresaID = @EmpresaId) AND stockActual <= stockMinimo";
        return connection.QuerySingle<int>(sql, new { EmpresaId = empresaId });
    }

    private static int ObtenerProductosSinStock(IConfiguration configuration, int? empresaId = null)
    {
        using var connection = CrearConexion(configuration);
        var sql = "SELECT COUNT(*) FROM Productos WHERE (@EmpresaId IS NULL OR empresaID = @EmpresaId) AND stockActual <= 0";
        return connection.QuerySingle<int>(sql, new { EmpresaId = empresaId });
    }

    private static List<VentaRegistro> ObtenerVentasPorRango(IConfiguration configuration, string range, int? empresaId = null)
    {
        var fechaInicio = range.ToLowerInvariant() switch
        {
            "dia" => DateTime.Now.AddDays(-1),
            "semana" => DateTime.Now.AddDays(-7),
            "mes" => DateTime.Now.AddMonths(-1),
            "anio" => DateTime.Now.AddYears(-1),
            _ => DateTime.Now.AddMonths(-1)
        };

        using var connection = CrearConexion(configuration);
        return connection.Query<VentaRegistro>(
            "SELECT * FROM Ventas WHERE productoId IN (SELECT id FROM Productos WHERE (@EmpresaId IS NULL OR empresaID = @EmpresaId)) AND fecha >= @FechaInicio ORDER BY fecha DESC",
            new { EmpresaId = empresaId, FechaInicio = fechaInicio }).ToList();
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
}