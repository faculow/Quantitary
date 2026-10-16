using Microsoft.AspNetCore.Http;

namespace Quantitary.Models
{
    public static class BD
    {
        private static readonly List<Usuario> Usuarios = new()
        {
            new Usuario { Id = 1, NombreUsuario = "admin", Contrasena = "123456", Mail = "admin@quantitary.com", NumeroTelefono = "111111111", EmpresaID = 1, RolID = 1 },
            new Usuario { Id = 2, NombreUsuario = "empresa", Contrasena = "admin123", Mail = "empresa@quantitary.com", NumeroTelefono = "222222222", EmpresaID = 1, RolID = 2 }
        };

        // 1. REGISTRARSE
        public static bool Registrarse(Usuario usuario)
        {
            // Validar primero si el mail ya está registrado
            if (string.IsNullOrWhiteSpace(usuario.Mail) || string.IsNullOrWhiteSpace(usuario.Contrasena))
            {
                return false;
            }

            if (Usuarios.Any(u => u.Mail.Equals(usuario.Mail, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            usuario.Id = Usuarios.Any() ? Usuarios.Max(u => u.Id) + 1 : 1;
            usuario.EmpresaID = usuario.EmpresaID > 0 ? usuario.EmpresaID : 1;
            usuario.RolID = usuario.RolID > 0 ? usuario.RolID : 1;
            Usuarios.Add(usuario);
            return true;
        }

        // 2. INICIAR SESIÓN (Verifica credenciales y guarda datos en la Session)
        public static bool IniciarSesion(string mail, string contrasena, HttpContext httpContext)
        {
            var usuario = Usuarios.FirstOrDefault(u =>
                u.Mail.Equals(mail.Trim(), StringComparison.OrdinalIgnoreCase) &&
                u.Contrasena == contrasena);

            if (usuario == null)
            {
                return false;
            }

            httpContext.Session.SetInt32("UsuarioID", usuario.Id);
            httpContext.Session.SetString("NombreUsuario", usuario.NombreUsuario);
            httpContext.Session.SetInt32("EmpresaID", usuario.EmpresaID);
            httpContext.Session.SetInt32("RolID", usuario.RolID);

            return true;
        }

        // 3. CERRAR SESIÓN
        public static void CerrarSesion(HttpContext httpContext)
        {
            httpContext.Session.Clear();
        }

        // 4. OBTENER ID DEL USUARIO LOGUEADO (Útil para validar vistas)
        public static int? ObtenerUsuarioLogueadoID(HttpContext httpContext)
        {
            return httpContext.Session.GetInt32("UsuarioID");
        }

        public static string? ObtenerNombreUsuario(HttpContext httpContext)
        {
            return httpContext.Session.GetString("NombreUsuario");
        }
    }
}