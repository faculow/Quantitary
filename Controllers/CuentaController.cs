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
            var empresaId = HttpContext.Session.GetInt32("EmpresaID") ?? 0;
            if (empresaId > 0 && !BD.TieneProductos(_configuration, empresaId))
            {
                return RedirectToAction("Importar", "Stock");
            }

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
            var errores = ModelState
                .Where(x => x.Value != null && x.Value.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors)
                .Select(e => e.ErrorMessage)
                .Distinct()
                .ToList();

            ViewBag.Error = errores.Any() ? string.Join(" ", errores) : "Verifica los datos ingresados.";
            return View(usuario);
        }

        if (string.IsNullOrWhiteSpace(usuario.NombreUsuario))
        {
            ViewBag.Error = "El nombre de usuario es obligatorio.";
            return View(usuario);
        }

        if (string.IsNullOrWhiteSpace(usuario.NombreEmpresa))
        {
            ViewBag.Error = "El nombre de la empresa es obligatorio.";
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

        if (string.IsNullOrWhiteSpace(usuario.NombreUsuario) || usuario.NombreUsuario.Length < 3)
        {
            ViewBag.Error = "El nombre de usuario debe tener al menos 3 caracteres.";
            return View(usuario);
        }

        if (usuario.Contrasena.Length < 6)
        {
            ViewBag.Error = "La contraseña debe tener al menos 6 caracteres.";
            return View(usuario);
        }

        if (!usuario.Contrasena.Any(char.IsDigit) || !usuario.Contrasena.Any(char.IsUpper) || !usuario.Contrasena.Any(char.IsLower))
        {
            ViewBag.Error = "La contraseña debe incluir mayúsculas, minúsculas y números.";
            return View(usuario);
        }

        if (string.IsNullOrWhiteSpace(usuario.Mail) || !usuario.Mail.Contains("@"))
        {
            ViewBag.Error = "El email no tiene un formato válido.";
            return View(usuario);
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
