using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Quantitary.Services;

/// <summary>Configuración de Claude. La clave viene de ANTHROPIC_API_KEY (variable de entorno o user-secrets).</summary>
public sealed class ClaudeOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-haiku-5-5";
    public string ApiVersion { get; set; } = "2026-10-21";
}

/// <summary>Valida la conversación, llama a la API oficial de Claude y convierte la respuesta en texto, tablas y gráficos.</summary>
public sealed class ClaudeChatService : IAiChatService
{
    public const int MaxTables = 4;
    public const int MaxRows = 50;
    public const int MaxColumns = 8;
    public const int MaxCharts = 4;
    public const int MaxLabels = 30;
    public const int MaxSeries = 4;
    // IAMensajes.Texto es nvarchar(4000); se deja margen para evitar fallos al guardar.
    public const int MaxReplyLength = 3800;

    private const int MaxTitleLength = 120;
    private const int MaxColumnLength = 80;
    private const int MaxCellLength = 300;
    private const int MaxLabelLength = 160;
    private const int MaxSeriesNameLength = 80;

    private const string ApiUrl = "https://api.anthropic.com/v1/messages";

    private readonly HttpClient _http;
    private readonly ClaudeOptions _options;
    private readonly ILogger<ClaudeChatService> _logger;

    public ClaudeChatService(HttpClient http, ClaudeOptions options, ILogger<ClaudeChatService> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public async Task<ChatResult> SendAsync(ChatRequest? request, string contexto, CancellationToken cancellationToken)
    {
        var message = request?.Message?.Trim();
        if (string.IsNullOrEmpty(message))
            return ChatResult.Failure("invalid_request", "Escribí una pregunta antes de enviar.");

        if (message.Length > AiChatLimits.MaxMessageLength)
            return ChatResult.Failure("invalid_request",
                $"El mensaje es demasiado largo. El límite es de {AiChatLimits.MaxMessageLength} caracteres.");

        var safeResponse = QuantitaryAiPolicy.TryHandleUnsafeRequest(message);
        if (safeResponse is not null) return safeResponse;

        if (string.IsNullOrWhiteSpace(_options.ApiKey) || _options.ApiKey.StartsWith("PEGAR", StringComparison.OrdinalIgnoreCase))
            return ChatResult.Failure("config_missing",
                "Falta la clave de Claude. Configurala con dotnet user-secrets o ANTHROPIC_API_KEY y volvé a iniciar la aplicación.");

        var history = NormalizeHistory(request?.History);
        var body = BuildRequestBody(history, message, contexto, _options.Model, request?.Image);

        var result = await CallClaudeAsync(body, cancellationToken);
        if (result.Ok && string.IsNullOrWhiteSpace(result.ChatTitle))
            result = result with { ChatTitle = TitleFromMessage(message) };
        return result;
    }

    /// <summary>Filtra, recorta y ordena el historial para que Claude reciba roles correctos y sin basura.</summary>
    private static List<(string Role, string Text)> NormalizeHistory(List<ChatTurn>? turns)
    {
        var result = new List<(string Role, string Text)>();
        if (turns is null) return result;

        foreach (var turn in turns.TakeLast(AiChatLimits.MaxHistoryTurns * 2))
        {
            if (turn is null) continue;
            if (turn.Role is not ("user" or "assistant")) continue;

            var text = turn.Text?.Trim();
            if (string.IsNullOrEmpty(text)) continue;
            if (text.Length > AiChatLimits.MaxTurnLength) text = text[..AiChatLimits.MaxTurnLength];

            result.Add((turn.Role, text));
        }

        if (result.Count > AiChatLimits.MaxHistoryTurns)
            result.RemoveRange(0, result.Count - AiChatLimits.MaxHistoryTurns);

        // La conversación debe empezar con un turno del usuario.
        while (result.Count > 0 && result[0].Role != "user")
            result.RemoveAt(0);

        return result;
    }

    private static JsonObject BuildRequestBody(
        List<(string Role, string Text)> history,
        string message,
        string contexto,
        string model,
        ChatImage? image)
    {
        var messages = new JsonArray();

        var shortContext = string.IsNullOrWhiteSpace(contexto) ? "No hay datos del comercio." : contexto.Trim();
        if (shortContext.Length > 24000)
            shortContext = shortContext[..24000] + "\n[Contexto recortado por seguridad]";

        messages.Add(MakeMessage("user", shortContext));

        foreach (var (role, text) in history.TakeLast(8))
            messages.Add(MakeMessage(role, text));

        messages.Add(MakeUserMessage(message, image));

        return new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = 1024,
            ["system"] = QuantitaryAiPolicy.SystemInstruction,
            ["messages"] = messages
        };
    }

    private static JsonObject MakeMessage(string role, string text) => new()
    {
        ["role"] = role,
        ["content"] = text
    };

    private static JsonObject MakeUserMessage(string text, ChatImage? image)
    {
        if (image is null) return MakeMessage("user", text);
        return new JsonObject
        {
            ["role"] = "user",
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "image",
                    ["source"] = new JsonObject
                    {
                        ["type"] = "base64",
                        ["media_type"] = image.MediaType,
                        ["data"] = image.Base64Data
                    }
                },
                new JsonObject { ["type"] = "text", ["text"] = text }
            }
        };
    }

    private async Task<ChatResult> CallClaudeAsync(JsonObject body, CancellationToken cancellationToken)
    {
        var payload = body.ToJsonString();

        // Los errores temporales de Anthropic se reintentan hasta 3 veces, con una pausa creciente.
        const int maxAttempts = 3;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                // La clave viaja en un encabezado, no en la URL, para que no quede en registros de URL.
                httpRequest.Headers.Add("x-api-key", _options.ApiKey);
                httpRequest.Headers.Add("anthropic-version", _options.ApiVersion);

                using var response = await _http.SendAsync(httpRequest, cancellationToken);
                var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
                var statusCode = (int)response.StatusCode;

                if ((statusCode == 500 || statusCode == 503 || statusCode == 529) && attempt < maxAttempts)
                {
                    _logger.LogWarning("Claude respondió HTTP {StatusCode}. Reintento {Attempt} de {Retries}.",
                        statusCode, attempt, maxAttempts - 1);
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken);
                    continue;
                }

                return ParseResponse(statusCode, responseText);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Claude no respondió dentro del tiempo límite.");
                return ChatResult.Failure("upstream_error",
                    "Claude tardó demasiado en responder. Intentá de nuevo en unos segundos.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("Error de red al comunicarse con Claude: {Message}", ex.Message);
                return ChatResult.Failure("upstream_error",
                    "No se pudo conectar con Claude. Revisá la conexión a internet e intentá de nuevo.");
            }
        }
    }

    private ChatResult ParseResponse(int statusCode, string responseText)
    {
        if (statusCode < 200 || statusCode > 299)
            return MapHttpError(statusCode, responseText);

        try
        {
            var root = JsonNode.Parse(responseText);

            var rawText = ExtractText(root);
            if (string.IsNullOrWhiteSpace(rawText))
                return ChatResult.Failure("empty_response",
                    "Claude respondió, pero la respuesta no se pudo usar. Probá reformular la pregunta.");

            var parsed = TryParsePlainText(rawText) ?? ParseModelOutput(rawText);
            return AddNavigationActions(parsed);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            _logger.LogWarning("No se pudo interpretar la respuesta de Claude.");
            return ChatResult.Failure("upstream_error",
                "Claude devolvió una respuesta que no se pudo leer. Intentá de nuevo.");
        }
    }

    private static ChatResult? TryParsePlainText(string rawText)
    {
        var text = StripCodeFence(rawText).Trim();
        if (string.IsNullOrEmpty(text)) return null;

        if (text.StartsWith("{", StringComparison.Ordinal) || text.StartsWith("[", StringComparison.Ordinal))
            return null;

        if (text.Contains('{') || text.Contains('['))
        {
            var candidate = text.Trim();
            if (candidate.Contains("\"respuesta\"", StringComparison.OrdinalIgnoreCase))
                return null;
        }

        return ChatResult.Success(text, new List<ChatTable>(), new List<ChatChart>());
    }

    private static readonly (string Url, string Label)[] NavigationRoutes =
    {
        ("/Stock/Importar", "Importar productos"),
        ("/Stock/Crear", "Agregar producto"),
        ("/Home/IndicadoresYOportunidades", "Ver indicadores"),
        ("/Home/AnalisisVentasGanancias", "Ver análisis"),
        ("/Home/AnalisisProducto", "Ver análisis de productos"),
        ("/Home/Historial", "Ver historial"),
        ("/Ajustes/Index", "Ir a Ajustes"),
        ("/Stock/Index", "Ir a Stock"),
        ("/Pdv/Index", "Ir al PDV")
    };

    /// <summary>Convierte únicamente rutas públicas conocidas en botones seguros y quita el marcado crudo del texto.</summary>
    private static ChatResult AddNavigationActions(ChatResult result)
    {
        if (!result.Ok || string.IsNullOrWhiteSpace(result.Reply)) return result;

        var reply = result.Reply.Replace("**", string.Empty, StringComparison.Ordinal);
        var actions = result.Actions?.ToList() ?? new List<ChatAction>();

        foreach (var (url, label) in NavigationRoutes)
        {
            if (!reply.Contains(url, StringComparison.OrdinalIgnoreCase)) continue;
            if (!actions.Any(action => action.Url.Equals(url, StringComparison.OrdinalIgnoreCase)))
                actions.Add(new ChatAction(label, url));

            var escaped = Regex.Escape(url);
            reply = Regex.Replace(reply, @"\[(?<label>[^\]]+)\]\(\s*" + escaped + @"\s*\)",
                match => match.Groups["label"].Value, RegexOptions.IgnoreCase);
            reply = Regex.Replace(reply, @"\s*\(\s*" + escaped + @"\s*\)", string.Empty, RegexOptions.IgnoreCase);
            reply = Regex.Replace(reply, @"(?<!\S)" + escaped + @"(?!\S)", string.Empty, RegexOptions.IgnoreCase);
        }

        reply = Regex.Replace(reply, @"[ \t]+\r?\n", Environment.NewLine).Trim();
        return result with { Reply = Limit(reply, MaxReplyLength), Actions = actions.Take(6).ToList() };
    }

    private static string ExtractText(JsonNode? root)
    {
        var content = root?["content"] as JsonArray;
        var builder = new StringBuilder();

        if (content is not null)
        {
            foreach (var block in content)
            {
                if (!string.Equals(block?["type"]?.GetValue<string>(), "text", StringComparison.Ordinal))
                    continue;

                var text = block?["text"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(text)) builder.Append(text);
            }
        }

        return builder.ToString().Trim();
    }

    /// <summary>
    /// Interpreta y valida el JSON del modelo. Falla cerrado: una salida con estructura
    /// inesperada nunca se muestra directamente al usuario.
    /// </summary>
    private static ChatResult ParseModelOutput(string rawText)
    {
        var cleaned = StripCodeFence(rawText);

        try
        {
            var obj = JsonNode.Parse(cleaned) as JsonObject;
            if (obj is null ||
                !HasOnlyKeys(obj, "respuesta", "titulo_chat", "tablas", "graficos") ||
                !IsString(obj["respuesta"]) ||
                !IsString(obj["titulo_chat"]) ||
                obj["tablas"] is not JsonArray ||
                obj["graficos"] is not JsonArray)
                return InvalidStructuredResponse();

            var reply = Limit(ToText(obj["respuesta"]), MaxReplyLength);
            var chatTitle = NormalizeChatTitle(ToText(obj["titulo_chat"]));
            var tables = ParseTables(obj["tablas"] as JsonArray);
            var charts = ParseCharts(obj["graficos"] as JsonArray);

            if (reply.Length == 0) return InvalidStructuredResponse();

            return ChatResult.Success(reply, tables, charts, chatTitle);
        }
        catch (JsonException)
        {
            return InvalidStructuredResponse();
        }
    }

    private static ChatResult InvalidStructuredResponse() =>
        ChatResult.Failure("invalid_model_output",
            "La IA generó una respuesta que no pasó los controles de seguridad. Intentá reformular la consulta.");

    private static bool HasOnlyKeys(JsonObject value, params string[] expected)
    {
        if (value.Count != expected.Length) return false;
        return expected.All(key => value.ContainsKey(key));
    }

    private static bool IsString(JsonNode? value) =>
        value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out _);

    private static string? NormalizeChatTitle(string title)
    {
        var clean = string.Join(" ", title.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Trim(' ', '"', '\'', '¿', '?', '¡', '!', '.', ',', ':', ';', '-', '–', '—');
        if (clean.Length == 0 || clean.Equals("Nuevo chat", StringComparison.OrdinalIgnoreCase)) return null;
        const int maxLength = 54;
        return clean.Length <= maxLength ? clean : clean[..maxLength].TrimEnd();
    }

    private static string TitleFromMessage(string message)
    {
        var clean = string.Join(" ", message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Trim(' ', '"', '\'', '¿', '?', '¡', '!', '.', ',', ':', ';', '-', '–', '—');
        if (clean.Length == 0) return "Nueva consulta";
        return clean.Length <= 54 ? clean : clean[..54].TrimEnd();
    }

    private static string StripCodeFence(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0) trimmed = trimmed[(firstNewline + 1)..];
            if (trimmed.EndsWith("```", StringComparison.Ordinal)) trimmed = trimmed[..^3];
        }
        return trimmed.Trim();
    }

    private static List<ChatTable> ParseTables(JsonArray? items)
    {
        var result = new List<ChatTable>();
        if (items is null) return result;

        foreach (var item in items.Take(MaxTables))
        {
            if (item is not JsonObject obj) continue;

            var columns = StringList(obj["columnas"] as JsonArray, MaxColumnLength).Take(MaxColumns).ToList();
            if (columns.Count == 0) continue;

            var rows = new List<List<string>>();
            if (obj["filas"] is JsonArray filas)
            {
                foreach (var fila in filas.Take(MaxRows))
                {
                    if (fila is not JsonArray cells) continue;

                    if (cells.Count != columns.Count) continue;
                    var row = cells.Select(cell => Limit(ToText(cell), MaxCellLength)).ToList();

                    rows.Add(row);
                }
            }

            result.Add(new ChatTable(Limit(ToText(obj["titulo"]), MaxTitleLength), columns, rows));
        }

        return result;
    }

    private static List<ChatChart> ParseCharts(JsonArray? items)
    {
        var result = new List<ChatChart>();
        if (items is null) return result;

        foreach (var item in items.Take(MaxCharts))
        {
            if (item is not JsonObject obj) continue;

            var chartType = ToText(obj["tipo"]).ToLowerInvariant() switch
            {
                "barras" => "bar",
                "lineas" => "line",
                "torta" => "pie",
                _ => null
            };
            if (chartType is null) continue;

            var labels = StringList(obj["etiquetas"] as JsonArray, MaxLabelLength).Take(MaxLabels).ToList();
            if (labels.Count == 0) continue;

            var series = new List<ChatChartSeries>();
            if (obj["series"] is JsonArray seriesItems)
            {
                foreach (var seriesItem in seriesItems.Take(MaxSeries))
                {
                    if (seriesItem is not JsonObject serie) continue;

                    if (serie["valores"] is not JsonArray valueItems || valueItems.Count != labels.Count) continue;
                    var values = valueItems.Select(ToNumber).ToList();
                    if (values.Any(value => value is null)) continue;

                    var name = Limit(ToText(serie["nombre"]), MaxSeriesNameLength);
                    if (name.Length == 0) name = "Valores";

                    series.Add(new ChatChartSeries(name, values));
                }
            }

            if (chartType == "pie") series = series.Take(1).ToList();
            if (series.Count == 0) continue;
            if (chartType == "pie" && series[0].Values.Any(value => value < 0)) continue;

            result.Add(new ChatChart(chartType, Limit(ToText(obj["titulo"]), MaxTitleLength), labels, series));
        }

        return result;
    }

    private static List<string> StringList(JsonArray? items, int maxLength) =>
        items is null
            ? new List<string>()
            : items.Select(item => Limit(ToText(item), maxLength)).Where(value => value.Length > 0).ToList();

    /// <summary>Convierte cualquier valor JSON simple en texto. Nunca devuelve HTML: el navegador lo muestra como texto.</summary>
    private static string ToText(JsonNode? node)
    {
        if (node is null) return string.Empty;

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text)) return text.Trim();
            if (value.TryGetValue<double>(out var number)) return number.ToString(CultureInfo.InvariantCulture);
            if (value.TryGetValue<bool>(out var flag)) return flag ? "Sí" : "No";
        }

        return string.Empty;
    }

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength].TrimEnd();

    private static double? ToNumber(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<double>(out var number) ? number : null;

    private ChatResult MapHttpError(int statusCode, string responseText)
    {
        // Solo registra el mensaje público de Anthropic; nunca la clave, el prompt ni el cuerpo completo.
        var apiMessage = ExtractApiErrorMessage(responseText);
        if (string.IsNullOrEmpty(apiMessage))
            _logger.LogWarning("Claude respondió con HTTP {StatusCode}.", statusCode);
        else
            _logger.LogWarning("Claude respondió con HTTP {StatusCode}: {ApiMessage}", statusCode, apiMessage);

        return statusCode switch
        {
            429 => ChatResult.Failure("rate_limited",
                "Se alcanzó temporalmente el límite de solicitudes de Claude. " +
                "Esperá un momento, revisá tus límites en Anthropic Console y volvé a intentar."),
            401 => ChatResult.Failure("auth_error",
                "Claude rechazó la clave configurada. Verificá que la clave sea válida."),
            403 => ChatResult.Failure("auth_error",
                "La clave configurada no tiene permiso para usar la API o el modelo de Claude."),
            404 => ChatResult.Failure("model_not_found",
                "El modelo de Claude configurado no está disponible. Revisá Claude:Model en appsettings.json."),
            413 => ChatResult.Failure("invalid_request",
                "La consulta es demasiado grande para Claude. Probá con menos datos o un mensaje más corto."),
            529 => ChatResult.Failure("upstream_error",
                "Claude está temporalmente sobrecargado. Intentá de nuevo en unos segundos."),
            _ => ChatResult.Failure("upstream_error",
                "Claude no pudo responder en este momento. Intentá de nuevo en unos segundos.")
        };
    }

    private static string ExtractApiErrorMessage(string responseText)
    {
        try
        {
            var message = JsonNode.Parse(responseText)?["error"]?["message"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrEmpty(message)) return string.Empty;

            message = message.Replace('\r', ' ').Replace('\n', ' ');
            return Limit(message, 300);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return string.Empty;
        }
    }
}


