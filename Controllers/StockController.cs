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

    [HttpGet]
    public IActionResult DescargarPlantillaProductos()
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var bytes = ExcelPlantillaHelper.GenerarPlantillaProductos();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "plantilla_productos.xlsx");
    }

    public IActionResult Index(string? sku = null, string? categoria = null)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var empresaId = HttpContext.Session.GetInt32("EmpresaID");
        var productos = _inventoryRepository.ObtenerProductos(sku, categoria, empresaId ?? 0);
        ViewBag.Categorias = _inventoryRepository.ObtenerCategorias(empresaId ?? 0);
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

        var empresaId = HttpContext.Session.GetInt32("EmpresaID");
        ViewBag.Categorias = _inventoryRepository.ObtenerCategorias(empresaId ?? 0);
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

        producto.EmpresaID = HttpContext.Session.GetInt32("EmpresaID") ?? 0;

        if (!ModelState.IsValid)
        {
            ViewBag.Categorias = _inventoryRepository.ObtenerCategorias(producto.EmpresaID);
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

    [HttpGet]
    public IActionResult Importar()
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Importar(IFormFile archivo)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        if (archivo == null || archivo.Length == 0)
        {
            TempData["MensajeError"] = "Debe seleccionar un archivo Excel para importar.";
            return RedirectToAction(nameof(Index));
        }

        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (extension != ".xlsx" && extension != ".xls")
        {
            TempData["MensajeError"] = "Formato no válido. Solo se permiten archivos Excel .xlsx o .xls.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            using var stream = archivo.OpenReadStream();
            var productos = ImportacionExcelHelper.LeerProductos(stream);

            var insertados = 0;
            var omitidos = 0;
            var errores = new List<string>();

            foreach (var producto in productos)
            {
                try
                {
                    _inventoryRepository.AgregarProducto(producto);
                    insertados++;
                }
                catch (Exception ex)
                {
                    omitidos++;
                    errores.Add($"SKU {producto.Sku}: {ex.Message}");
                }
            }

            if (insertados > 0 && errores.Count == 0)
            {
                TempData["MensajeExito"] = $"Importación exitosa. Se registraron {insertados} productos.";
                return RedirectToAction(nameof(Index));
            }

            if (insertados > 0)
            {
                TempData["MensajeAdvertencia"] = $"Se importaron {insertados} productos. Hubo {omitidos} filas descartadas: {string.Join(" | ", errores.Take(3))}";
                return RedirectToAction(nameof(Index));
            }

            TempData["MensajeError"] = $"No se pudo importar ningún producto. {string.Join(" | ", errores.Take(5))}";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            TempData["MensajeError"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["MensajeError"] = "Ocurrió un error al leer el archivo Excel. Verifique que el formato sea correcto y que las columnas coincidan con el modelo requerido.";
            return RedirectToAction(nameof(Index));
        }
    }
}

