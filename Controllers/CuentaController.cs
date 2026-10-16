using Microsoft.AspNetCore.Mvc;
using Quantitary.Models;

namespace Quantitary.Controllers
{
    public class CuentaController : Controller
    {
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

            var exito = BD.IniciarSesion(mail, contrasena, HttpContext);
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

            var creado = BD.Registrarse(usuario);
            if (creado)
            {
                BD.IniciarSesion(usuario.Mail, usuario.Contrasena, HttpContext);
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "El correo ya está registrado.";
            return View(usuario);
        }

        public IActionResult Logout()
        {
            BD.CerrarSesion(HttpContext);
            return RedirectToAction(nameof(Login));
        }
    }
}
