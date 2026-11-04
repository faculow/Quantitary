using Dapper;
using Microsoft.Data.SqlClient;
using Quantitary.Models;

namespace Quantitary.Services;

public sealed class IAHistorialSql
{
    private readonly IConfiguration _config;
    public IAHistorialSql(IConfiguration config) => _config = config;
    private SqlConnection Conexion() => new(BD.ObtenerCadenaConexion(_config));

    public async Task<bool> UsuarioValido(int usuario, int empresa)
    {
        await using var db = Conexion();
        return await db.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.Usuarios WHERE id=@usuario AND empresaID=@empresa", new { usuario, empresa }) == 1;
    }
    public async Task<object> Listar(int usuario, int empresa)
    {
        await using var db = Conexion();
        return await db.QueryAsync("SELECT Id, Titulo, ActualizadoEn FROM dbo.IAChats WHERE UsuarioId=@usuario AND EmpresaId=@empresa ORDER BY ActualizadoEn DESC", new { usuario, empresa });
    }
    public async Task<Guid> Crear(int usuario, int empresa)
    {
        var id = Guid.NewGuid();
        await using var db = Conexion();
        await db.ExecuteAsync("INSERT INTO dbo.IAChats(Id, UsuarioId, EmpresaId, Titulo) VALUES (@id,@usuario,@empresa,N'Nuevo chat')", new { id, usuario, empresa });
        return id;
    }
    public async Task<bool> Existe(Guid chat, int usuario, int empresa)
    {
        await using var db = Conexion();
        return await db.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.IAChats WHERE Id=@chat AND UsuarioId=@usuario AND EmpresaId=@empresa", new { chat, usuario, empresa }) == 1;
    }
    public async Task<List<ChatTurn>> Leer(Guid chat, int usuario, int empresa)
    {
        await using var db = Conexion();
        var data = await db.QueryAsync<ChatRow>(@"SELECT m.Rol, m.Texto AS Text FROM dbo.IAMensajes m INNER JOIN dbo.IAChats c ON c.Id=m.ChatId WHERE c.Id=@chat AND c.UsuarioId=@usuario AND c.EmpresaId=@empresa ORDER BY m.Id", new { chat, usuario, empresa });
        return data.Select(x => new ChatTurn(x.Rol, x.Text)).ToList();
    }
    public async Task GuardarIntercambio(Guid chat, int usuario, int empresa, string pregunta, string respuesta)
    {
        await using var db = Conexion();
        await db.OpenAsync();
        await using var tx = (SqlTransaction)await db.BeginTransactionAsync();
        var affected = await db.ExecuteAsync(@"UPDATE dbo.IAChats SET ActualizadoEn=SYSUTCDATETIME(), Titulo=CASE WHEN Titulo=N'Nuevo chat' THEN LEFT(@pregunta, 120) ELSE Titulo END WHERE Id=@chat AND UsuarioId=@usuario AND EmpresaId=@empresa", new { chat, usuario, empresa, pregunta }, tx);
        if (affected != 1) throw new UnauthorizedAccessException();
        await db.ExecuteAsync("INSERT INTO dbo.IAMensajes(ChatId,Rol,Texto) VALUES (@chat,'user',@pregunta),(@chat,'assistant',@respuesta)", new { chat, pregunta, respuesta }, tx);
        await tx.CommitAsync();
    }
    public async Task<bool> Renombrar(Guid chat, int usuario, int empresa, string titulo)
    {
        await using var db = Conexion();
        return await db.ExecuteAsync("UPDATE dbo.IAChats SET Titulo=@titulo, ActualizadoEn=SYSUTCDATETIME() WHERE Id=@chat AND UsuarioId=@usuario AND EmpresaId=@empresa", new { chat, usuario, empresa, titulo = titulo.Trim() }) == 1;
    }
    public async Task<bool> Eliminar(Guid chat, int usuario, int empresa)
    {
        await using var db = Conexion();
        return await db.ExecuteAsync("DELETE FROM dbo.IAChats WHERE Id=@chat AND UsuarioId=@usuario AND EmpresaId=@empresa", new { chat, usuario, empresa }) == 1;
    }
    private sealed class ChatRow { public string? Rol { get; set; } public string? Text { get; set; } }
}
