using Microsoft.AspNetCore.Mvc;
using Quantitary.Models;

namespace Quantitary.Controllers;

public class StockController : Controller
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

    public IActionResult Index(string? sku = null, string? categoria = null)
    {
        var productos = ObtenerProductos(sku, categoria);
        ViewBag.Categorias = ObtenerCategorias();
        ViewBag.Sku = sku;
        ViewBag.Categoria = categoria;
        return View(productos);
    }

    [HttpGet]
    public IActionResult Crear()
    {
        ViewBag.Categorias = ObtenerCategorias();
        return View();
    }

    [HttpPost]
    public IActionResult Crear(Producto producto)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Categorias = ObtenerCategorias();
            return View(producto);
        }

        AgregarProducto(producto);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Editar(int id)
    {
        var producto = _productos.FirstOrDefault(p => p.Id == id);
        if (producto == null) return NotFound();

        ViewBag.Categorias = ObtenerCategorias();
        return View(producto);
    }

    [HttpPost]
    public IActionResult Editar(int id, Producto producto)
    {
        var actualizado = ActualizarProducto(id, producto);
        if (actualizado == null) return NotFound();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Eliminar(int id)
    {
        EliminarProducto(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult AgregarStock(int id, int cantidad, decimal precioUnitario)
    {
        AgregarStockProducto(id, cantidad, precioUnitario);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Importar(IFormFile archivo)
    {
        if (archivo == null || archivo.Length == 0)
        {
            TempData["Mensaje"] = "Debe seleccionar un archivo para importar.";
            return RedirectToAction(nameof(Index));
        }

        CargarCatalogo(archivo);
        TempData["Mensaje"] = "Catálogo importado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    private List<Producto> ObtenerProductos(string? sku = null, string? categoria = null)
    {
        IEnumerable<Producto> query = _productos;

        if (!string.IsNullOrWhiteSpace(sku))
        {
            query = query.Where(p => p.Sku.Contains(sku, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(categoria))
        {
            query = query.Where(p => p.Categoria.Contains(categoria, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderBy(p => p.Nombre).ToList();
    }

    private Producto AgregarProducto(Producto producto)
    {
        if (string.IsNullOrWhiteSpace(producto.Nombre) || string.IsNullOrWhiteSpace(producto.Sku))
        {
            throw new ArgumentException("El nombre y el SKU son obligatorios.");
        }

        producto.Id = _productos.Any() ? _productos.Max(p => p.Id) + 1 : 1;
        producto.FechaCarga = DateTime.Now;
        _productos.Add(producto);
        return producto;
    }

    private Producto? ActualizarProducto(int id, Producto producto)
    {
        var actual = _productos.FirstOrDefault(p => p.Id == id);
        if (actual == null) return null;

        actual.Nombre = producto.Nombre;
        actual.Sku = producto.Sku;
        actual.Categoria = producto.Categoria;
        actual.Descripcion = producto.Descripcion;
        actual.Precio = producto.Precio;
        actual.StockActual = producto.StockActual;
        actual.StockMinimo = producto.StockMinimo;
        actual.Proveedor = producto.Proveedor;
        actual.CostoCompra = producto.CostoCompra;
        actual.PrecioVenta = producto.PrecioVenta ?? producto.Precio;

        return actual;
    }

    private bool EliminarProducto(int id)
    {
        var producto = _productos.FirstOrDefault(p => p.Id == id);
        if (producto == null) return false;

        _productos.Remove(producto);
        return true;
    }

    private Producto? AgregarStockProducto(int id, int cantidad, decimal precioUnitario)
    {
        var producto = _productos.FirstOrDefault(p => p.Id == id);
        if (producto == null || cantidad <= 0) return null;

        producto.StockActual += cantidad;
        producto.CostoCompra = precioUnitario;
        return producto;
    }

    private List<Producto> CargarCatalogo(IFormFile archivo)
    {
        using var reader = new StreamReader(archivo.OpenReadStream());
        var contenido = reader.ReadToEnd();
        var lineas = contenido.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        var productosAgregados = new List<Producto>();

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
                Proveedor = "Catalogo importado"
            };

            if (!string.IsNullOrWhiteSpace(producto.Nombre) && !string.IsNullOrWhiteSpace(producto.Sku))
            {
                AgregarProducto(producto);
                productosAgregados.Add(producto);
            }
        }

        return productosAgregados;
    }

    private List<string> ObtenerCategorias()
    {
        return _productos.Select(p => p.Categoria).Distinct().OrderBy(x => x).ToList();
    }
}

