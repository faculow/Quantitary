using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Quantitary.Models;

namespace Quantitary.Controllers;

public class HomeController : Controller
{
    private readonly ProductoRepository _inventoryRepository;

    public HomeController(ProductoRepository inventoryRepository)
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

    public IActionResult Index()
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var empresaId = HttpContext.Session.GetInt32("EmpresaID") ?? 0;
        if (empresaId > 0 && !_inventoryRepository.TieneProductos(empresaId))
        {
            ViewBag.MensajeAdvertencia = "Primero debes cargar tu catálogo inicial desde Excel para poder continuar.";
        }

        var nombre = HttpContext.Session.GetString("NombreUsuario") ?? "Usuario";
        var nombreEmpresa = HttpContext.Session.GetString("NombreEmpresa") ?? "Mi empresa";
        ViewBag.NombreUsuario = nombre;
        ViewBag.NombreEmpresa = nombreEmpresa;
        ViewBag.Dashboard = _inventoryRepository.ObtenerDashboard("mes", empresaId);
        return View();
    }

    public IActionResult Historial(string rango = "mes", string? nombreProducto = null, string? sku = null, string? categoria = null)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var empresaId = HttpContext.Session.GetInt32("EmpresaID") ?? 0;
        if (empresaId > 0 && !_inventoryRepository.TieneProductos(empresaId))
        {
            ViewBag.MensajeAdvertencia = "Primero debes cargar tu catálogo inicial desde Excel para poder continuar.";
        }

        ViewBag.Dashboard = _inventoryRepository.ObtenerDashboard(rango, empresaId, nombreProducto, sku, categoria);
        ViewBag.RangoActual = rango;
        ViewBag.NombreEmpresa = HttpContext.Session.GetString("NombreEmpresa") ?? "Mi empresa";
        ViewBag.Categorias = _inventoryRepository.ObtenerCategorias(empresaId);
        ViewBag.NombreProducto = nombreProducto;
        ViewBag.Sku = sku;
        ViewBag.Categoria = categoria;
        return View();
    }

    public IActionResult Privacy()
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
