using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Quantitary.Models;

namespace Quantitary.Controllers;

public class HomeController : Controller
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

    public IActionResult Index()
    {
        var nombre = HttpContext.Session.GetString("NombreUsuario") ?? "Usuario";
        ViewBag.NombreUsuario = nombre;
        ViewBag.Dashboard = ObtenerDashboard("mes");
        return View();
    }

    public IActionResult Historial(string rango = "mes")
    {
        ViewBag.Dashboard = ObtenerDashboard(rango);
        ViewBag.RangoActual = rango;
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private DashboardSummary ObtenerDashboard(string range)
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
