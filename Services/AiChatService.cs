namespace Quantitary.Services;

/// <summary>
/// Contrato independiente del proveedor. El controlador usa esta interfaz y no
/// necesita saber si la respuesta viene de Gemini, Claude u otro modelo.
/// </summary>
public interface IAiChatService
{
    Task<ChatResult> SendAsync(ChatRequest? request, string contexto, CancellationToken cancellationToken);
}

public static class AiChatLimits
{
    public const int MaxMessageLength = 2000;
    public const int MaxHistoryTurns = 20;
    public const int MaxTurnLength = 4000;
}

/// <summary>Un turno del historial. Role: "user" o "assistant".</summary>
public sealed record ChatTurn(string? Role, string? Text);

/// <summary>Solicitud que envía el navegador a POST /api/chat.</summary>
public sealed record ChatRequest(string? Message, List<ChatTurn>? History, Guid? ChatId = null);

public sealed record ChatError(string Code, string Message);

/// <summary>Tabla lista para dibujar en el chat.</summary>
public sealed record ChatTable(string Title, List<string> Columns, List<List<string>> Rows);

public sealed record ChatChartSeries(string Name, List<double?> Values);

/// <summary>Gráfico listo para dibujar. Type: "bar", "line" o "pie".</summary>
public sealed record ChatChart(string Type, string Title, List<string> Labels, List<ChatChartSeries> Series);

/// <summary>Respuesta controlada que recibe el navegador. Nunca incluye secretos ni errores crudos del proveedor.</summary>
public sealed record ChatResult(bool Ok, string? Reply, ChatError? Error, List<ChatTable>? Tables, List<ChatChart>? Charts, string? ChatTitle)
{
    public static ChatResult Success(string reply, List<ChatTable> tables, List<ChatChart> charts, string? chatTitle = null) =>
        new(true, reply, null, tables, charts, chatTitle);

    public static ChatResult Failure(string code, string message) =>
        new(false, null, new ChatError(code, message), null, null, null);
}
