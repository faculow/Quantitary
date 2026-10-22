using Microsoft.AspNetCore.Mvc;
using Quantitary.Models;

namespace Quantitary.Controllers;

public class StockController : Controller
{
    private readonly InventoryRepository _inventoryRepository;

    public StockController(InventoryRepository inventoryRepository)
    {
        _inventoryRepository = inventoryRepository;
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

    public IActionResult Index(string? sku = null, string? categoria = null)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var productos = _inventoryRepository.ObtenerProductos(sku, categoria);
        ViewBag.Categorias = _inventoryRepository.ObtenerCategorias();
        ViewBag.Sku = sku;
        ViewBag.Categoria = categoria;
        return View(productos);
    }

    [HttpGet]
    public IActionResult Crear()
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        ViewBag.Categorias = _inventoryRepository.ObtenerCategorias();
        return View();
    }

    [HttpPost]
    public IActionResult Crear(Producto producto)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Categorias = _inventoryRepository.ObtenerCategorias();
            return View(producto);
        }

        _inventoryRepository.AgregarProducto(producto);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Editar(int id)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var producto = _inventoryRepository.BuscarPorId(id);
        if (producto == null)
        {
            return NotFound();
        }

        ViewBag.Categorias = _inventoryRepository.ObtenerCategorias();
        return View(producto);
    }

    [HttpPost]
    public IActionResult Editar(int id, Producto producto)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var actualizado = _inventoryRepository.ActualizarProducto(id, producto);
        if (!actualizado)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Eliminar(int id)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        _inventoryRepository.EliminarProducto(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult AgregarStock(int id, int cantidad, decimal precioUnitario)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        _inventoryRepository.AgregarStock(id, cantidad, precioUnitario);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Importar(IFormFile archivo)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        if (archivo == null || archivo.Length == 0)
        {
            TempData["Mensaje"] = "Debe seleccionar un archivo para importar.";
            return RedirectToAction(nameof(Index));
        }

        using var reader = new StreamReader(archivo.OpenReadStream());
        var contenido = reader.ReadToEnd();
        var lineas = contenido.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

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
                Proveedor = "Catalogo importado",
                CostoCompra = 0m,
                PrecioVenta = 0m
            };

            if (!string.IsNullOrWhiteSpace(producto.Nombre) && !string.IsNullOrWhiteSpace(producto.Sku))
            {
                _inventoryRepository.AgregarProducto(producto);
            }
        }

        TempData["Mensaje"] = "Catálogo importado correctamente.";
        return RedirectToAction(nameof(Index));
    }
}

