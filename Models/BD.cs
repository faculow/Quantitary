using System.Data;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace Quantitary.Models;

public static class BD
{
    private static string NormalizarMail(string? mail)
    {
        return (mail ?? string.Empty).Trim();
    }

    public static string ObtenerCadenaConexion(IConfiguration configuration)
    {
        return configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta la cadena de conexión DefaultConnection.");
    }

    public static IDbConnection CrearConexion(IConfiguration configuration)
    {
        return new SqlConnection(ObtenerCadenaConexion(configuration));
    }

    public static bool Registrarse(Usuario usuario, IConfiguration configuration)
    {
        var mailNormalizado = NormalizarMail(usuario.Mail);

        if (string.IsNullOrWhiteSpace(usuario.NombreUsuario)
            || string.IsNullOrWhiteSpace(mailNormalizado)
            || string.IsNullOrWhiteSpace(usuario.Contrasena))
        {
            return false;
        }

        using var connection = CrearConexion(configuration);
        connection.Open();

        var empresaId = usuario.EmpresaID > 0
            ? usuario.EmpresaID
            : connection.QuerySingleOrDefault<int?>("SELECT TOP 1 id FROM Empresa ORDER BY id");

        var rolId = usuario.RolID > 0
            ? usuario.RolID
            : connection.QuerySingleOrDefault<int?>("SELECT TOP 1 id FROM Rol ORDER BY id");

        if (empresaId == null || rolId == null)
        {
            return false;
        }

        var existe = connection.QuerySingleOrDefault<int>(
            "SELECT 1 FROM Usuarios WHERE LOWER(LTRIM(RTRIM(mail))) = @Mail",
            new { Mail = mailNormalizado.ToLowerInvariant() });

        if (existe == 1)
        {
            return false;
        }

        var query = @"
            INSERT INTO Usuarios (nombreUsuario, contrasena, mail, numeroTelefono, empresaID, rolID)
            VALUES (@NombreUsuario, @Contrasena, @Mail, @NumeroTelefono, @EmpresaID, @RolID)";

        connection.Execute(query, new
        {
            NombreUsuario = usuario.NombreUsuario.Trim(),
            Contrasena = usuario.Contrasena,
            Mail = mailNormalizado,
            NumeroTelefono = usuario.NumeroTelefono,
            EmpresaID = empresaId.Value,
            RolID = rolId.Value
        });

        return true;
    }

    public static bool IniciarSesion(string mail, string contrasena, HttpContext httpContext, IConfiguration configuration)
    {
        var mailNormalizado = NormalizarMail(mail);

        using var connection = CrearConexion(configuration);
        connection.Open();

        var usuario = connection.QuerySingleOrDefault<Usuario>(
            "SELECT TOP 1 * FROM Usuarios WHERE LOWER(LTRIM(RTRIM(mail))) = @Mail AND contrasena = @Contrasena",
            new { Mail = mailNormalizado.ToLowerInvariant(), Contrasena = contrasena });

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

    public static void CerrarSesion(HttpContext httpContext)
    {
        httpContext.Session.Clear();
    }

    public static int? ObtenerUsuarioLogueadoID(HttpContext httpContext)
    {
        return httpContext.Session.GetInt32("UsuarioID");
    }

    public static string? ObtenerNombreUsuario(HttpContext httpContext)
    {
        return httpContext.Session.GetString("NombreUsuario");
    }
}