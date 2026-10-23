using Microsoft.AspNetCore.Mvc;
using Quantitary.Models;

namespace Quantitary.Controllers;

public class CuentaController : Controller
{
    private readonly IConfiguration _configuration;

    public CuentaController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (BD.ObtenerUsuarioLogueadoID(HttpContext) != null)
        {
            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    [HttpPost]
    public IActionResult Login(string mail, string contrasena)
    {
        if (string.IsNullOrWhiteSpace(mail) || string.IsNullOrWhiteSpace(contrasena))
        {
            ViewBag.Error = "Completa todos los campos.";
            return View();
        }

        var exito = BD.IniciarSesion(mail, contrasena, HttpContext, _configuration);
        if (exito)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewBag.Error = "Correo o contraseña incorrectos.";
        return View();
    }

    [HttpGet]
    public IActionResult Registro()
    {
        if (BD.ObtenerUsuarioLogueadoID(HttpContext) != null)
        {
            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    [HttpPost]
    public IActionResult Registro(Usuario usuario)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Error = "Verifica los datos ingresados.";
            return View(usuario);
        }

        if (string.IsNullOrWhiteSpace(usuario.NombreUsuario))
        {
            ViewBag.Error = "El nombre de usuario es obligatorio.";
            return View(usuario);
        }

        if (string.IsNullOrWhiteSpace(usuario.Mail))
        {
            ViewBag.Error = "El correo es obligatorio.";
            return View(usuario);
        }

        if (string.IsNullOrWhiteSpace(usuario.Contrasena))
        {
            ViewBag.Error = "La contraseña es obligatoria.";
            return View(usuario);
        }

        if (usuario.EmpresaID <= 0)
        {
            usuario.EmpresaID = 0;
        }

        if (usuario.RolID <= 0)
        {
            usuario.RolID = 1;
        }

        var creado = BD.Registrarse(usuario, _configuration);
        if (creado)
        {
            BD.IniciarSesion(usuario.Mail, usuario.Contrasena, HttpContext, _configuration);
            return RedirectToAction("Importar", "Stock");
        }

        ViewBag.Error = "No se pudo crear la cuenta. Verifica que el correo no esté registrado y que los datos sean válidos.";
        return View(usuario);
    }

    [HttpGet]
    [HttpPost]
    public IActionResult Logout()
    {
        BD.CerrarSesion(HttpContext);
        return RedirectToAction(nameof(Login));
    }
}
