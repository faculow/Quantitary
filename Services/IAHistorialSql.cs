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
    public async Task<IReadOnlyList<ChatResumen>> Listar(int usuario, int empresa)
    {
        await using var db = Conexion();
        var chats = await db.QueryAsync<ChatResumen>(
            "SELECT Id, Titulo, ActualizadoEn FROM dbo.IAChats WHERE UsuarioId=@usuario AND EmpresaId=@empresa ORDER BY ActualizadoEn DESC",
            new { usuario, empresa });
        return chats.ToList();
    }
    public async Task<Guid> Crear(int usuario, int empresa)
    {
        var id = Guid.NewGuid();
        await using var db = Conexion();
        await db.ExecuteAsync(@"INSERT INTO dbo.IAChats(Id, UsuarioId, EmpresaId, Titulo, CreadoEn, ActualizadoEn)
            VALUES (@id,@usuario,@empresa,N'Nuevo chat',SYSUTCDATETIME(),SYSUTCDATETIME())", new { id, usuario, empresa });
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
    public async Task GuardarIntercambio(Guid chat, int usuario, int empresa, string pregunta, string respuesta, string? tituloSugerido)
    {
        await using var db = Conexion();
        await db.OpenAsync();
        await using var tx = (SqlTransaction)await db.BeginTransactionAsync();
        var titulo = NormalizarTitulo(tituloSugerido) ?? CrearTitulo(pregunta);
        var affected = await db.ExecuteAsync(@"UPDATE dbo.IAChats SET ActualizadoEn=SYSUTCDATETIME(),
            Titulo=CASE WHEN LTRIM(RTRIM(Titulo))=N'Nuevo chat' OR LTRIM(RTRIM(Titulo))=N'' THEN @titulo ELSE Titulo END
            WHERE Id=@chat AND UsuarioId=@usuario AND EmpresaId=@empresa", new { chat, usuario, empresa, titulo }, tx);
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
        await db.OpenAsync();
        await using var tx = (SqlTransaction)await db.BeginTransactionAsync();
        await db.ExecuteAsync(@"DELETE m FROM dbo.IAMensajes m
            INNER JOIN dbo.IAChats c ON c.Id=m.ChatId
            WHERE c.Id=@chat AND c.UsuarioId=@usuario AND c.EmpresaId=@empresa", new { chat, usuario, empresa }, tx);
        var eliminado = await db.ExecuteAsync(
            "DELETE FROM dbo.IAChats WHERE Id=@chat AND UsuarioId=@usuario AND EmpresaId=@empresa",
            new { chat, usuario, empresa }, tx) == 1;
        await tx.CommitAsync();
        return eliminado;
    }
    private static string CrearTitulo(string pregunta)
    {
        var limpio = string.Join(" ", pregunta.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
        limpio = limpio.Trim('¿', '?', '¡', '!', '.', ',', ':', ';', '-', ' ');
        if (limpio.Length == 0) return "Nuevo chat";
        const int maximo = 54;
        return limpio.Length <= maximo ? limpio : limpio[..(maximo - 1)].TrimEnd() + "…";
    }
    private static string? NormalizarTitulo(string? titulo)
    {
        if (string.IsNullOrWhiteSpace(titulo)) return null;
        var limpio = string.Join(" ", titulo.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Trim(' ', '\"', '\'', '¿', '?', '¡', '!', '.', ',', ':', ';', '-', '–', '—');
        if (limpio.Length == 0 || limpio.Equals("Nuevo chat", StringComparison.OrdinalIgnoreCase)) return null;
        const int maximo = 54;
        return limpio.Length <= maximo ? limpio : limpio[..maximo].TrimEnd();
    }
    public sealed class ChatResumen
    {
        public Guid Id { get; set; }
        public string Titulo { get; set; } = "Nuevo chat";
        public DateTime ActualizadoEn { get; set; }
    }
    private sealed class ChatRow { public string? Rol { get; set; } public string? Text { get; set; } }
}
