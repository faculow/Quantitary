using Dapper;
using Microsoft.AspNetCore.Mvc;
using Quantitary.Models;

namespace Quantitary.Controllers;

public sealed class AjustesController : Controller
{
    private static readonly HashSet<string> Secciones =
        new(StringComparer.OrdinalIgnoreCase) { "perfil", "empresa", "notificaciones", "apariencia", "seguridad" };

    private readonly IConfiguration _configuration;

    public AjustesController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private (int Usuario, int Empresa)? Sesion()
    {
        var usuario = HttpContext.Session.GetInt32("UsuarioID") ?? 0;
        var empresa = HttpContext.Session.GetInt32("EmpresaID") ?? 0;
        return usuario > 0 && empresa > 0 ? (usuario, empresa) : null;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? seccion)
    {
        var sesion = Sesion();
        if (sesion is null) return RedirectToAction("Login", "Cuenta");

        using var connection = BD.CrearConexion(_configuration);
        var datos = await connection.QuerySingleOrDefaultAsync<DatosAjustes>(
            @"SELECT u.nombreUsuario, u.mail, u.numeroTelefono, e.nombre AS NombreEmpresa,
                     CAST(CASE WHEN ISNULL(p.gestionarUsuarios, 0) = 1 THEN 1 ELSE 0 END AS bit) AS PuedeEditarEmpresa
              FROM Usuarios u
              INNER JOIN Empresa e ON e.id = u.empresaID
              LEFT JOIN Rol r ON r.id = u.rolID
              LEFT JOIN Permisos p ON p.id = r.permisoID
              WHERE u.id = @Usuario AND u.empresaID = @Empresa",
            new { Usuario = sesion.Value.Usuario, Empresa = sesion.Value.Empresa });

        if (datos is null)
        {
            BD.CerrarSesion(HttpContext);
            return RedirectToAction("Login", "Cuenta");
        }

        var activa = Secciones.Contains(seccion ?? string.Empty) ? seccion!.ToLowerInvariant() : "perfil";
        return View(new AjustesViewModel
        {
            SeccionActiva = activa,
            PuedeEditarEmpresa = datos.PuedeEditarEmpresa,
            Perfil = new PerfilAjustesInput
            {
                NombreUsuario = datos.NombreUsuario,
                Mail = datos.Mail,
                NumeroTelefono = datos.NumeroTelefono
            },
            Empresa = new EmpresaAjustesInput { Nombre = datos.NombreEmpresa }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarPerfil([Bind(Prefix = "Perfil")] PerfilAjustesInput perfil)
    {
        var sesion = Sesion();
        if (sesion is null) return RedirectToAction("Login", "Cuenta");

        if (!ModelState.IsValid)
            return await VistaConErrores("perfil", perfil: perfil);

        var mail = perfil.Mail.Trim().ToLowerInvariant();
        var nombre = perfil.NombreUsuario.Trim();
        using var connection = BD.CrearConexion(_configuration);
        var repetidos = await connection.QuerySingleAsync<DatosRepetidos>(
            @"SELECT
                  COUNT(CASE WHEN LOWER(LTRIM(RTRIM(mail))) = @Mail THEN 1 END) AS Mail,
                  COUNT(CASE WHEN LOWER(LTRIM(RTRIM(nombreUsuario))) = LOWER(@Nombre) THEN 1 END) AS Nombre
              FROM Usuarios
              WHERE id <> @Usuario",
            new { Mail = mail, Nombre = nombre, Usuario = sesion.Value.Usuario });

        if (repetidos.Mail > 0)
            ModelState.AddModelError("Perfil.Mail", "Ese correo ya está registrado.");

        if (repetidos.Nombre > 0)
            ModelState.AddModelError("Perfil.NombreUsuario", "Ese nombre de usuario ya está registrado.");

        if (!ModelState.IsValid)
            return await VistaConErrores("perfil", perfil: perfil);

        await connection.ExecuteAsync(
            @"UPDATE Usuarios
              SET nombreUsuario = @Nombre, mail = @Mail, numeroTelefono = @Telefono
              WHERE id = @Usuario AND empresaID = @Empresa",
            new
            {
                Nombre = nombre,
                Mail = mail,
                Telefono = string.IsNullOrWhiteSpace(perfil.NumeroTelefono) ? null : perfil.NumeroTelefono.Trim(),
                Usuario = sesion.Value.Usuario,
                Empresa = sesion.Value.Empresa
            });

        HttpContext.Session.SetString("NombreUsuario", nombre);
        TempData["AjustesExito"] = "Tus datos personales se guardaron correctamente.";
        return RedirectToAction(nameof(Index), new { seccion = "perfil" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarEmpresa([Bind(Prefix = "Empresa")] EmpresaAjustesInput empresa)
    {
        var sesion = Sesion();
        if (sesion is null) return RedirectToAction("Login", "Cuenta");

        using var connection = BD.CrearConexion(_configuration);
        if (!await PuedeEditarEmpresa(connection, sesion.Value.Usuario, sesion.Value.Empresa))
            return Forbid();

        if (!ModelState.IsValid)
            return await VistaConErrores("empresa", empresa: empresa);

        var nombre = empresa.Nombre.Trim();
        await connection.ExecuteAsync(
            "UPDATE Empresa SET nombre = @Nombre WHERE id = @Empresa",
            new { Nombre = nombre, Empresa = sesion.Value.Empresa });

        HttpContext.Session.SetString("NombreEmpresa", nombre);
        TempData["AjustesExito"] = "Los datos de la empresa se guardaron correctamente.";
        return RedirectToAction(nameof(Index), new { seccion = "empresa" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarContrasena([Bind(Prefix = "Seguridad")] ContrasenaAjustesInput seguridad)
    {
        var sesion = Sesion();
        if (sesion is null) return RedirectToAction("Login", "Cuenta");

        if (!ModelState.IsValid)
            return await VistaConErrores("seguridad", seguridad: seguridad);

        if (!seguridad.ContrasenaNueva.Any(char.IsLower) ||
            !seguridad.ContrasenaNueva.Any(char.IsUpper) ||
            !seguridad.ContrasenaNueva.Any(char.IsDigit))
        {
            ModelState.AddModelError("Seguridad.ContrasenaNueva",
                "La contraseña debe incluir mayúsculas, minúsculas y números.");
            return await VistaConErrores("seguridad", seguridad: seguridad);
        }

        if (string.Equals(seguridad.ContrasenaActual, seguridad.ContrasenaNueva, StringComparison.Ordinal))
        {
            ModelState.AddModelError("Seguridad.ContrasenaNueva",
                "La nueva contraseña debe ser diferente de la actual.");
            return await VistaConErrores("seguridad", seguridad: seguridad);
        }

        using var connection = BD.CrearConexion(_configuration);
        var actual = await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT contrasena FROM Usuarios WHERE id = @Usuario AND empresaID = @Empresa",
            new { Usuario = sesion.Value.Usuario, Empresa = sesion.Value.Empresa });

        if (actual is null || !string.Equals(actual, seguridad.ContrasenaActual, StringComparison.Ordinal))
        {
            ModelState.AddModelError("Seguridad.ContrasenaActual", "La contraseña actual no es correcta.");
            return await VistaConErrores("seguridad", seguridad: seguridad);
        }

        await connection.ExecuteAsync(
            "UPDATE Usuarios SET contrasena = @Nueva WHERE id = @Usuario AND empresaID = @Empresa",
            new
            {
                Nueva = seguridad.ContrasenaNueva,
                Usuario = sesion.Value.Usuario,
                Empresa = sesion.Value.Empresa
            });

        TempData["AjustesExito"] = "La contraseña se cambió correctamente.";
        return RedirectToAction(nameof(Index), new { seccion = "seguridad" });
    }

    private async Task<IActionResult> VistaConErrores(
        string seccion,
        PerfilAjustesInput? perfil = null,
        EmpresaAjustesInput? empresa = null,
        ContrasenaAjustesInput? seguridad = null)
    {
        var resultado = await Index(seccion);
        if (resultado is ViewResult view && view.Model is AjustesViewModel model)
        {
            if (perfil is not null) model.Perfil = perfil;
            if (empresa is not null) model.Empresa = empresa;
            if (seguridad is not null) model.Seguridad = seguridad;
        }
        return resultado;
    }

    private static async Task<bool> PuedeEditarEmpresa(
        System.Data.IDbConnection connection, int usuario, int empresa)
    {
        return await connection.ExecuteScalarAsync<int>(
            @"SELECT COUNT(1)
              FROM Usuarios u
              INNER JOIN Rol r ON r.id = u.rolID
              INNER JOIN Permisos p ON p.id = r.permisoID
              WHERE u.id = @usuario AND u.empresaID = @empresa AND p.gestionarUsuarios = 1",
            new { usuario, empresa }) == 1;
    }

    private sealed class DatosAjustes
    {
        public string NombreUsuario { get; init; } = string.Empty;
        public string Mail { get; init; } = string.Empty;
        public string? NumeroTelefono { get; init; }
        public string NombreEmpresa { get; init; } = string.Empty;
        public bool PuedeEditarEmpresa { get; init; }
    }

    private sealed class DatosRepetidos
    {
        public int Mail { get; init; }
        public int Nombre { get; init; }
    }
}
