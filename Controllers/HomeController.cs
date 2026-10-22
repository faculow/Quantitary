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

    public IActionResult Index()
    {
        var nombre = HttpContext.Session.GetString("NombreUsuario") ?? "Usuario";
        ViewBag.NombreUsuario = nombre;
        ViewBag.Dashboard = _inventoryRepository.ObtenerDashboard("mes");
        return View();
    }

    public IActionResult Historial(string rango = "mes")
    {
        ViewBag.Dashboard = _inventoryRepository.ObtenerDashboard(rango);
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
}
