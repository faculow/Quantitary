using Dapper;
using Microsoft.Data.SqlClient;
using Quantitary.Models;
using System.Text.Json;

namespace Quantitary.Services;

public sealed class IAHistorialSql
{
    private readonly IConfiguration _config;
    public IAHistorialSql(IConfiguration config) => _config = config;
    public async Task<bool> UsuarioValido(int usuario, int empresa)
        => await BD.IAUsuarioValidoAsync(_config, usuario, empresa);
    public async Task<IReadOnlyList<ChatResumen>> Listar(int usuario, int empresa)
        => (await BD.IAListarChatsAsync(_config, usuario, empresa)).Select(x => new ChatResumen(x.Id, x.Titulo, x.ActualizadoEn)).ToList();
    public async Task<Guid> Crear(int usuario, int empresa)
        => await BD.IACrearChatAsync(_config, usuario, empresa);
    public async Task<bool> Existe(Guid chat, int usuario, int empresa)
        => await BD.IAExisteChatAsync(_config, chat, usuario, empresa);
    public async Task<List<ChatTurn>> Leer(Guid chat, int usuario, int empresa)
        => (await BD.IALeerChatAsync(_config, chat, usuario, empresa)).Select(ConvertirTurno).ToList();

    public async Task GuardarIntercambio(Guid chat, int usuario, int empresa, string pregunta, ChatResult resultado)
    {
        var contenido = new ContenidoEnriquecido(
            resultado.Tables ?? new List<ChatTable>(),
            resultado.Charts ?? new List<ChatChart>(),
            resultado.Actions ?? new List<ChatAction>());

        var contenidoJson = JsonSerializer.Serialize(contenido);
        await BD.GuardarIntercambioAsync(
            _config,
            chat,
            usuario,
            empresa,
            pregunta,
            resultado.Reply ?? string.Empty,
            resultado.ChatTitle,
            contenidoJson);
    }
    public async Task<bool> Renombrar(Guid chat, int usuario, int empresa, string titulo)
        => await BD.IARenombrarChatAsync(_config, chat, usuario, empresa, titulo);
    public async Task<bool> Eliminar(Guid chat, int usuario, int empresa)
        => await BD.IAEliminarChatAsync(_config, chat, usuario, empresa);

    private static ChatTurn ConvertirTurno(BD.IAMensaje mensaje)
    {
        var role = mensaje.Rol ?? "user";
        if (!role.Equals("assistant", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(mensaje.ContenidoJson))
            return new ChatTurn(role, mensaje.Text ?? string.Empty);

        try
        {
            var contenido = JsonSerializer.Deserialize<ContenidoEnriquecido>(mensaje.ContenidoJson);
            return new ChatTurn(
                role,
                mensaje.Text ?? string.Empty,
                contenido?.Tables ?? new List<ChatTable>(),
                contenido?.Charts ?? new List<ChatChart>(),
                contenido?.Actions ?? new List<ChatAction>());
        }
        catch (JsonException)
        {
            // Un registro antiguo o dañado nunca debe impedir abrir el chat.
            return new ChatTurn(role, mensaje.Text ?? string.Empty);
        }
    }

    private sealed record ContenidoEnriquecido(
        List<ChatTable> Tables,
        List<ChatChart> Charts,
        List<ChatAction> Actions);

    public sealed record ChatResumen(Guid Id, string Titulo, DateTime ActualizadoEn);
}
