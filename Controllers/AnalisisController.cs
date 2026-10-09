using Microsoft.AspNetCore.Mvc;
using Quantitary.Models;

namespace Quantitary.Controllers
{
    public class CuentaController : Controller
    {
        [HttpGet]
        public IActionResult Login()
        {
            // Si el usuario ya está logueado, lo redirigimos al Home
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
                ViewBag.Error = "Por favor, complete todos los campos.";
                return View();
            }

            bool exito = BD.IniciarSesion(mail, contrasena, HttpContext);

            if (exito)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "El correo o la contraseña son incorrectos.";
            return View();
        }

        [HttpGet]
        public IActionResult Registro()
        {
            // Si el usuario ya está logueado, lo redirigimos al Home
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
                ViewBag.Error = "Por favor, verifique los datos ingresados.";
                return View(usuario);
            }

            // Intentar registrar el usuario en la BD
            bool creado = BD.Registrarse(usuario);

            if (creado)
            {
                // Iniciar sesión automáticamente tras un registro exitoso
                BD.IniciarSesion(usuario.Mail, usuario.Contrasena, HttpContext);
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "El correo electrónico ya está registrado o no se pudo crear la cuenta.";
            return View(usuario);
        }


        public IActionResult Logout()
        {
            BD.CerrarSesion(HttpContext);
            return RedirectToAction("Login");
        }
    }
}