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
            TempData.Remove("MensajeExito");
            TempData.Remove("MensajeError");
            TempData["MensajeAdvertencia"] = "Primero debes cargar tu catálogo inicial desde Excel para poder continuar.";
            return RedirectToAction("Importar", "Stock");
        }

        var nombre = HttpContext.Session.GetString("NombreUsuario") ?? "Usuario";
        var nombreEmpresa = HttpContext.Session.GetString("NombreEmpresa") ?? "Mi empresa";
        ViewBag.NombreUsuario = nombre;
        ViewBag.NombreEmpresa = nombreEmpresa;
        ViewBag.Dashboard = _inventoryRepository.ObtenerDashboard("mes", empresaId);
        return View();
    }

    public IActionResult Historial(string rango = "mes", string? busqueda = null)
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        var empresaId = HttpContext.Session.GetInt32("EmpresaID") ?? 0;
        if (empresaId > 0 && !_inventoryRepository.TieneProductos(empresaId))
        {
            TempData.Remove("MensajeExito");
            TempData.Remove("MensajeError");
            TempData["MensajeAdvertencia"] = "Primero debes cargar tu catálogo inicial desde Excel para poder continuar.";
            return RedirectToAction("Importar", "Stock");
        }

        var dashboard = _inventoryRepository.ObtenerDashboard(rango, empresaId);
        var busquedaNormalizada = busqueda?.Trim();

        if (!string.IsNullOrWhiteSpace(busquedaNormalizada))
        {
            dashboard.UltimosProductosVendidos = dashboard.UltimosProductosVendidos
                .Where(v =>
                    v.NombreProducto.Contains(busquedaNormalizada, StringComparison.OrdinalIgnoreCase)
                    || v.Sku.Contains(busquedaNormalizada, StringComparison.OrdinalIgnoreCase)
                    || v.Categoria.Contains(busquedaNormalizada, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        ViewBag.Dashboard = dashboard;
        ViewBag.RangoActual = rango;
        ViewBag.Busqueda = busquedaNormalizada;
        ViewBag.NombreEmpresa = HttpContext.Session.GetString("NombreEmpresa") ?? "Mi empresa";
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
