using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Quantitary.Models;

namespace Quantitary.Controllers;

public class PdvController : Controller
{
    private readonly ProductoRepository _productoRepository;
    private readonly IConfiguration _configuration;

    public PdvController(ProductoRepository productoRepository, IConfiguration configuration)
    {
        _productoRepository = productoRepository;
        _configuration = configuration;
    }

    private bool HaySesionActiva()
    {
        return HttpContext.Session.GetInt32("UsuarioID") != null;
    }

    private IActionResult? RedirigirSiNoHaySesion()
    {
        if (!HaySesionActiva())
        {
            return RedirectToAction("Login", "Cuenta");
        }

        return null;
    }

    private List<PdvCarritoItem> ObtenerCarrito()
    {
        var contenido = HttpContext.Session.GetString("PdvCarrito");
        if (string.IsNullOrWhiteSpace(contenido))
        {
            return new List<PdvCarritoItem>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<PdvCarritoItem>>(contenido) ?? new List<PdvCarritoItem>();
        }
        catch
        {
            return new List<PdvCarritoItem>();
        }
    }

    private void GuardarCarrito(List<PdvCarritoItem> carrito)
    {
        HttpContext.Session.SetString("PdvCarrito", JsonSerializer.Serialize(carrito));
    }

    public IActionResult Index(string? filtro = null, string? categoria = null)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var empresaId = HttpContext.Session.GetInt32("EmpresaID") ?? 0;
        var productos = _productoRepository.ObtenerProductos(null, categoria, empresaId)
            .Where(p => string.IsNullOrWhiteSpace(filtro)
                || p.Nombre.Contains(filtro, StringComparison.OrdinalIgnoreCase)
                || p.Sku.Contains(filtro, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Nombre)
            .ToList();

        var model = new PdvIndexViewModel
        {
            Productos = productos,
            Carrito = ObtenerCarrito(),
            Categorias = _productoRepository.ObtenerCategorias(empresaId),
            Filtro = filtro,
            Categoria = categoria
        };

        return View(model);
    }

    [HttpPost]
    public IActionResult Agregar(int id)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var producto = _productoRepository.BuscarPorId(id);
        if (producto == null)
        {
            TempData["PdvError"] = "El producto no existe.";
            return RedirectToAction(nameof(Index));
        }

        var carrito = ObtenerCarrito();
        var item = carrito.FirstOrDefault(x => x.ProductoId == id);

        if (item == null)
        {
            if (producto.StockActual <= 0)
            {
                TempData["PdvError"] = $"No hay stock disponible para {producto.Nombre}.";
                return RedirectToAction(nameof(Index));
            }

            carrito.Add(new PdvCarritoItem
            {
                ProductoId = producto.Id,
                Nombre = producto.Nombre,
                Sku = producto.Sku,
                Categoria = producto.Categoria,
                Marca = producto.Marca,
                PrecioUnitario = producto.Precio,
                StockActual = producto.StockActual,
                Cantidad = 1
            });
        }
        else
        {
            if (item.Cantidad >= producto.StockActual)
            {
                TempData["PdvError"] = $"No puedes agregar más unidades de {producto.Nombre}.";
                return RedirectToAction(nameof(Index));
            }

            item.Cantidad++;
        }

        GuardarCarrito(carrito);
        TempData["PdvSuccess"] = $"{producto.Nombre} agregado al carrito.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Quitar(int id)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var carrito = ObtenerCarrito();
        var item = carrito.FirstOrDefault(x => x.ProductoId == id);
        if (item != null)
        {
            carrito.Remove(item);
            GuardarCarrito(carrito);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult ActualizarCantidad(int id, int cantidad)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        if (cantidad <= 0)
        {
            return RedirectToAction(nameof(Index));
        }

        var producto = _productoRepository.BuscarPorId(id);
        if (producto == null)
        {
            return RedirectToAction(nameof(Index));
        }

        var carrito = ObtenerCarrito();
        var item = carrito.FirstOrDefault(x => x.ProductoId == id);
        if (item == null)
        {
            return RedirectToAction(nameof(Index));
        }

        item.Cantidad = Math.Min(cantidad, producto.StockActual);
        GuardarCarrito(carrito);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Cobrar(string metodoPago)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var carrito = ObtenerCarrito();
        if (carrito.Count == 0)
        {
            TempData["PdvError"] = "No hay productos en la venta.";
            return RedirectToAction(nameof(Index));
        }

        var metodo = string.IsNullOrWhiteSpace(metodoPago) ? "Efectivo" : metodoPago.Trim();
        var empresaId = HttpContext.Session.GetInt32("EmpresaID") ?? 0;

        using var connection = BD.CrearConexion(_configuration);
        connection.Open();

        foreach (var item in carrito)
        {
            var producto = _productoRepository.BuscarPorId(item.ProductoId);
            if (producto == null)
            {
                TempData["PdvError"] = $"El producto {item.Nombre} ya no existe.";
                return RedirectToAction(nameof(Index));
            }

            if (producto.StockActual < item.Cantidad)
            {
                TempData["PdvError"] = $"No hay suficiente stock de {producto.Nombre}.";
                return RedirectToAction(nameof(Index));
            }

            var ventaId = connection.ExecuteScalar<int>(
                @"INSERT INTO Ventas (productoId, nombreProducto, fecha, metodoPago, cantidad, precio, costo)
                  VALUES (@ProductoId, @NombreProducto, @Fecha, @MetodoPago, @Cantidad, @Precio, @Costo);
                  SELECT CAST(SCOPE_IDENTITY() as int);",
                new
                {
                    ProductoId = producto.Id,
                    NombreProducto = producto.Nombre,
                    Fecha = DateTime.Now,
                    MetodoPago = metodo,
                    Cantidad = item.Cantidad,
                    Precio = item.PrecioUnitario * item.Cantidad,
                    Costo = (producto.CostoCompra > 0 ? producto.CostoCompra : producto.Precio) * item.Cantidad
                });

            connection.Execute(
                @"INSERT INTO DetalleVenta (ventaID, productoID, precioVenta, precioCompra, porcentajeGanancia)
                  VALUES (@VentaId, @ProductoId, @PrecioVenta, @PrecioCompra, @PorcentajeGanancia)",
                new
                {
                    VentaId = ventaId,
                    ProductoId = producto.Id,
                    PrecioVenta = item.PrecioUnitario * item.Cantidad,
                    PrecioCompra = (producto.CostoCompra > 0 ? producto.CostoCompra : producto.Precio) * item.Cantidad,
                    PorcentajeGanancia = producto.CostoCompra > 0 ? ((item.PrecioUnitario - producto.CostoCompra) / producto.CostoCompra) * 100m : 0m
                });

            producto.StockActual -= item.Cantidad;
            producto.CantidadVentas += item.Cantidad;
            _productoRepository.ActualizarProducto(producto.Id, producto);
        }

        HttpContext.Session.SetString("UltimaVentaTimestamp", DateTime.Now.ToString("O"));
        HttpContext.Session.Remove("PdvCarrito");
        TempData["PdvSuccess"] = $"Venta registrada con pago por {metodo}.";
        return RedirectToAction(nameof(Index));
    }
}
