using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Quantitary.Models;

namespace Quantitary.Controllers;

public class StockController : Controller
{
    private readonly ProductoRepository _inventoryRepository;

    public StockController(ProductoRepository inventoryRepository)
    {
        _inventoryRepository = inventoryRepository;
    }

    private const string ClaveImportacionPendiente = "PendingImportProductos";

    private void GuardarImportacionPendiente(List<Producto> productos)
    {
        HttpContext.Session.SetString(ClaveImportacionPendiente, JsonSerializer.Serialize(productos));
    }

    private List<Producto> ObtenerImportacionPendiente()
    {
        var valor = HttpContext.Session.GetString(ClaveImportacionPendiente);
        if (string.IsNullOrWhiteSpace(valor))
        {
            return new List<Producto>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<Producto>>(valor) ?? new List<Producto>();
        }
        catch
        {
            return new List<Producto>();
        }
    }

    private void BorrarImportacionPendiente()
    {
        HttpContext.Session.Remove(ClaveImportacionPendiente);
    }

    private void LimpiarMensajesTempData()
    {
        TempData.Remove("MensajeExito");
        TempData.Remove("MensajeAdvertencia");
        TempData.Remove("MensajeError");
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

    public IActionResult Index(string? sku = null, string? categoria = null, bool pocoStock = false, decimal? precioMinimo = null, string? orden = null)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var empresaId = HttpContext.Session.GetInt32("EmpresaID");
        var productos = _inventoryRepository.ObtenerProductos(sku, categoria, pocoStock, precioMinimo, orden, empresaId ?? 0);
        ViewBag.Categorias = _inventoryRepository.ObtenerCategorias(empresaId ?? 0);
        ViewBag.Sku = sku;
        ViewBag.Categoria = categoria;
        ViewBag.PocoStock = pocoStock;
        ViewBag.PrecioMinimo = precioMinimo;
        ViewBag.Orden = orden;

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return PartialView("_ProductosTable", productos);
        }

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

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return PartialView("Editar", producto);
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

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { success = true });
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
    public IActionResult ImportarConfirmado(string? modo, string[]? skusRenombradas = null, string[]? nuevosNombres = null, string[]? skusDuplicados = null)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        if (string.IsNullOrWhiteSpace(modo) || string.Equals(modo, "cancelar", StringComparison.OrdinalIgnoreCase))
        {
            BorrarImportacionPendiente();
            LimpiarMensajesTempData();
            TempData["MensajeAdvertencia"] = "La importación fue cancelada.";
            return RedirectToAction(nameof(Index));
        }

        var productosImportados = ObtenerImportacionPendiente();
        if (productosImportados.Count == 0)
        {
            LimpiarMensajesTempData();
            TempData["MensajeError"] = "No se encontró una importación pendiente para confirmar.";
            return RedirectToAction(nameof(Index));
        }

        var empresaId = BD.ObtenerEmpresaIdActual(_inventoryRepository.GetConfiguration(), HttpContext);
        var productosExistentes = _inventoryRepository.ObtenerProductos(null, null, empresaId);

        try
        {
            if (modo.Equals("renombrar", StringComparison.OrdinalIgnoreCase))
            {
                var renombres = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (skusRenombradas != null && nuevosNombres != null)
                {
                    for (var i = 0; i < Math.Min(skusRenombradas.Length, nuevosNombres.Length); i++)
                    {
                        var sku = (skusRenombradas[i] ?? string.Empty).Trim();
                        var nombreNuevo = (nuevosNombres[i] ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(sku) && !string.IsNullOrWhiteSpace(nombreNuevo))
                        {
                            renombres[sku] = nombreNuevo;
                        }
                    }
                }

                foreach (var producto in productosImportados)
                {
                    var sku = (producto.Sku ?? string.Empty).Trim();
                    if (renombres.TryGetValue(sku, out var nombreRenombrado) && !string.IsNullOrWhiteSpace(nombreRenombrado))
                    {
                        producto.Nombre = nombreRenombrado;
                    }
                }

                var insertados = 0;
                var errores = new List<string>();
                foreach (var producto in productosImportados)
                {
                    producto.EmpresaID = empresaId;
                    if (producto.StockActual < 0) producto.StockActual = 0;
                    if (producto.StockMinimo < 0) producto.StockMinimo = 0;

                    try
                    {
                        _inventoryRepository.AgregarProducto(producto);
                        insertados++;
                    }
                    catch (Exception ex)
                    {
                        errores.Add($"SKU {producto.Sku}: {ex.Message}");
                    }
                }

                BorrarImportacionPendiente();
                LimpiarMensajesTempData();
                if (insertados > 0)
                {
                    TempData["MensajeExito"] = $"Se importaron {insertados} productos con nombres ajustados.";
                    return RedirectToAction(nameof(Index));
                }

                TempData["MensajeError"] = $"No se pudo importar ningún producto. {string.Join(" | ", errores.Take(5))}";
                return RedirectToAction(nameof(Index));
            }

            if (modo.Equals("sumar", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var producto in productosImportados)
                {
                    var existente = productosExistentes.FirstOrDefault(p =>
                        string.Equals((p.Sku ?? string.Empty).Trim(), (producto.Sku ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));

                    if (existente != null)
                    {
                        existente.StockActual += producto.StockActual;
                        existente.Categoria = string.IsNullOrWhiteSpace(producto.Categoria) ? existente.Categoria : producto.Categoria;
                        existente.Proveedor = string.IsNullOrWhiteSpace(producto.Proveedor) ? existente.Proveedor : producto.Proveedor;
                        existente.Precio = producto.Precio > 0 ? producto.Precio : existente.Precio;
                        _inventoryRepository.ActualizarProducto(existente.Id, existente);
                    }
                    else
                    {
                        producto.EmpresaID = empresaId;
                        _inventoryRepository.AgregarProducto(producto);
                    }
                }

                BorrarImportacionPendiente();
                LimpiarMensajesTempData();
                TempData["MensajeExito"] = "Se sumaron los stocks de los productos duplicados y se importaron los nuevos.";
                return RedirectToAction(nameof(Index));
            }

            if (modo.Equals("reemplazar", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var producto in productosImportados)
                {
                    var existente = productosExistentes.FirstOrDefault(p =>
                        string.Equals((p.Sku ?? string.Empty).Trim(), (producto.Sku ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));

                    if (existente != null)
                    {
                        existente.Nombre = producto.Nombre;
                        existente.Sku = producto.Sku;
                        existente.Categoria = producto.Categoria;
                        existente.Descripcion = producto.Descripcion;
                        existente.Precio = producto.Precio;
                        existente.StockActual = producto.StockActual;
                        existente.StockMinimo = producto.StockMinimo;
                        existente.Proveedor = producto.Proveedor;
                        existente.EmpresaID = empresaId;
                        _inventoryRepository.ActualizarProducto(existente.Id, existente);
                    }
                    else
                    {
                        producto.EmpresaID = empresaId;
                        _inventoryRepository.AgregarProducto(producto);
                    }
                }

                BorrarImportacionPendiente();
                LimpiarMensajesTempData();
                TempData["MensajeExito"] = "Se reemplazaron los productos duplicados y se importaron los nuevos.";
                return RedirectToAction(nameof(Index));
            }

            BorrarImportacionPendiente();
            LimpiarMensajesTempData();
            TempData["MensajeAdvertencia"] = "La importación fue cancelada.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            BorrarImportacionPendiente();
            LimpiarMensajesTempData();
            TempData["MensajeError"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
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
            var empresaId = BD.ObtenerEmpresaIdActual(_inventoryRepository.GetConfiguration(), HttpContext);

            if (empresaId <= 0)
            {
                throw new InvalidOperationException("No se pudo identificar la empresa activa. Inicie sesión nuevamente.");
            }

            var productosExistentes = _inventoryRepository.ObtenerProductos(null, null, empresaId);
            var importacion = ImportacionExcelHelper.AnalizarImportacion(productos, productosExistentes);

            if (importacion.Conflictos.Count > 0 || importacion.ConflictosPorNombre.Count > 0)
            {
                GuardarImportacionPendiente(productos);
                ViewBag.Conflictos = importacion.Conflictos;
                ViewBag.ConflictosPorNombre = importacion.ConflictosPorNombre;
                ViewBag.ProductosNuevos = importacion.Productos;
                ViewBag.NombreArchivo = archivo.FileName;
                return View(nameof(Importar));
            }

            BorrarImportacionPendiente();

            var insertados = 0;
            var omitidos = 0;
            var errores = new List<string>();

            foreach (var producto in productos)
            {
                producto.EmpresaID = empresaId;
                if (producto.StockActual < 0) producto.StockActual = 0;
                if (producto.StockMinimo < 0) producto.StockMinimo = 0;

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
                TempData.Remove("MensajeAdvertencia");
                TempData.Remove("MensajeError");
                TempData["MensajeExito"] = $"Importación exitosa. Se registraron {insertados} productos.";
                return RedirectToAction(nameof(Index));
            }

            if (insertados > 0)
            {
                TempData.Remove("MensajeAdvertencia");
                TempData.Remove("MensajeError");
                TempData["MensajeAdvertencia"] = $"Se importaron {insertados} productos. Hubo {omitidos} filas descartadas: {string.Join(" | ", errores.Take(3))}";
                return RedirectToAction(nameof(Index));
            }

            TempData.Remove("MensajeAdvertencia");
            TempData.Remove("MensajeExito");
            TempData["MensajeError"] = $"No se pudo importar ningún producto. {string.Join(" | ", errores.Take(5))}";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            TempData.Remove("MensajeAdvertencia");
            TempData.Remove("MensajeExito");
            TempData["MensajeError"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData.Remove("MensajeAdvertencia");
            TempData.Remove("MensajeExito");
            TempData["MensajeError"] = "Ocurrió un error al leer el archivo Excel. Verifique que el formato sea correcto y que las columnas coincidan con el modelo requerido.";
            return RedirectToAction(nameof(Index));
        }
    }

    private static string? NormalizarFiltro(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        return valor.Trim();
    }

    [HttpGet]
    public IActionResult Filtrar(string? sku = null, string? categoria = null, bool pocoStock = false, decimal? precioMinimo = null, string? orden = null)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var empresaId = HttpContext.Session.GetInt32("EmpresaID");
        var skuNormalizado = NormalizarFiltro(sku);
        var categoriaNormalizada = NormalizarFiltro(categoria);

        var productos = _inventoryRepository.ObtenerProductos(
            skuNormalizado,
            categoriaNormalizada,
            pocoStock,
            precioMinimo,
            orden,
            empresaId ?? 0);

        return PartialView("_ProductosTable", productos);
    }
}

