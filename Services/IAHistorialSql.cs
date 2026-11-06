using Dapper;
using Microsoft.Data.SqlClient;
using Quantitary.Models;

namespace Quantitary.Services;

public sealed class IAHistorialSql
{
    private readonly IConfiguration _config;
    public IAHistorialSql(IConfiguration config) => _config = config;
    public async Task<bool> UsuarioValido(int usuario, int empresa)
        => await BD.IAUsuarioValidoAsync(_config, usuario, empresa);
    public async Task<IReadOnlyList<ChatResumen>> Listar(int usuario, int empresa)
        => (await BD.IAListarChatsAsync(_config, usuario, empresa)).Select(x => new ChatResumen(x.Titulo, x.ActualizadoEn)).ToList();
    public async Task<Guid> Crear(int usuario, int empresa)
        => await BD.IACrearChatAsync(_config, usuario, empresa);
    public async Task<bool> Existe(Guid chat, int usuario, int empresa)
        => await BD.IAExisteChatAsync(_config, chat, usuario, empresa);
    public async Task<List<ChatTurn>> Leer(Guid chat, int usuario, int empresa)
        => (await BD.IALeerChatAsync(_config, chat, usuario, empresa)).Select(x => new ChatTurn(x.Rol ?? "user", x.Text ?? string.Empty)).ToList();
    public async Task GuardarIntercambio(Guid chat, int usuario, int empresa, string pregunta, string respuesta, string? tituloSugerido)
        => await BD.GuardarIntercambioAsync(_config, chat, usuario, empresa, pregunta, respuesta, tituloSugerido);
    public async Task<bool> Renombrar(Guid chat, int usuario, int empresa, string titulo)
        => await BD.IARenombrarChatAsync(_config, chat, usuario, empresa, titulo);
    public async Task<bool> Eliminar(Guid chat, int usuario, int empresa)
        => await BD.IAEliminarChatAsync(_config, chat, usuario, empresa);
    public sealed record ChatResumen(string Titulo, DateTime ActualizadoEn);
    public sealed record ChatTurn(string Rol, string Text);
}
