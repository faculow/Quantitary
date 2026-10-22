using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Quantitary.Models;

namespace Quantitary.Controllers;

public class HomeController : Controller
{
    private readonly InventoryRepository _inventoryRepository;

    public HomeController(InventoryRepository inventoryRepository)
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

        var nombre = HttpContext.Session.GetString("NombreUsuario") ?? "Usuario";
        ViewBag.NombreUsuario = nombre;
        ViewBag.Dashboard = _inventoryRepository.ObtenerDashboard("mes");
        return View();
    }

    public IActionResult Historial(string rango = "mes")
    {
        var redirect = RedirigirSiNoHaySesion();
        if (redirect != null)
        {
            return redirect;
        }

        ViewBag.Dashboard = _inventoryRepository.ObtenerDashboard(rango);
        ViewBag.RangoActual = rango;
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
