using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Quantitary.Services;

/// <summary>Configuración de Gemini. La clave viene de GEMINI_API_KEY (variable de entorno o user-secrets).</summary>
public sealed class GeminiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-3.6-flash";
}

/// <summary>Valida la conversación, llama a la API oficial de Gemini y convierte la respuesta en texto, tablas y gráficos.</summary>
public sealed class GeminiChatService : IAiChatService
{
    public const int MaxTables = 4;
    public const int MaxRows = 50;
    public const int MaxColumns = 8;
    public const int MaxCharts = 4;
    public const int MaxLabels = 30;
    public const int MaxSeries = 4;
    public const int MaxReplyLength = 6000;

    private const int MaxTitleLength = 120;
    private const int MaxColumnLength = 80;
    private const int MaxCellLength = 300;
    private const int MaxLabelLength = 160;
    private const int MaxSeriesNameLength = 80;

    private const string ApiBase = "https://generativelanguage.googleapis.com/v1beta/models/";

    private readonly HttpClient _http;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiChatService> _logger;

    public GeminiChatService(HttpClient http, GeminiOptions options, ILogger<GeminiChatService> logger)
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
                "Falta la clave de Gemini. Configurala con dotnet user-secrets o GEMINI_API_KEY y volvé a iniciar la aplicación.");

        var history = NormalizeHistory(request?.History);
        var body = BuildRequestBody(history, message, contexto);

        return await CallGeminiAsync(body, cancellationToken);
    }

    /// <summary>Filtra, recorta y ordena el historial para que Gemini reciba roles correctos y sin basura.</summary>
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

    private static JsonObject BuildRequestBody(List<(string Role, string Text)> history, string message, string contexto)
    {
        var contents = new JsonArray();

        // Los datos pueden contener texto escrito por usuarios (por ejemplo, nombres de productos).
        // Se envían como contenido no confiable, nunca como instrucción del sistema.
        contents.Add(MakeContent("user", QuantitaryAiPolicy.WrapBusinessData(contexto)));
        contents.Add(MakeContent("model", "Contexto recibido como datos de solo lectura. Mantendré las reglas de Quantitary."));

        foreach (var (role, text) in history)
            contents.Add(MakeContent(role == "assistant" ? "model" : "user", text));

        contents.Add(MakeContent("user", message));

        return new JsonObject
        {
            ["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray(new JsonObject { ["text"] = QuantitaryAiPolicy.SystemInstruction })
            },
            ["safetySettings"] = new JsonArray(
                SafetySetting("HARM_CATEGORY_HATE_SPEECH", "BLOCK_MEDIUM_AND_ABOVE"),
                SafetySetting("HARM_CATEGORY_HARASSMENT", "BLOCK_MEDIUM_AND_ABOVE"),
                SafetySetting("HARM_CATEGORY_SEXUALLY_EXPLICIT", "BLOCK_MEDIUM_AND_ABOVE"),
                SafetySetting("HARM_CATEGORY_DANGEROUS_CONTENT", "BLOCK_MEDIUM_AND_ABOVE")),
            ["contents"] = contents,
            ["generationConfig"] = new JsonObject
            {
                ["temperature"] = 0.15,
                ["topP"] = 0.8,
                ["candidateCount"] = 1,
                ["maxOutputTokens"] = 4096,
                // El tipo JSON es compatible con generateContent. La estructura exacta se
                // exige en el prompt y se valida de forma estricta antes de usar la respuesta.
                ["responseMimeType"] = "application/json"
            }
        };
    }

    private static JsonObject SafetySetting(string category, string threshold) => new()
    {
        ["category"] = category,
        ["threshold"] = threshold
    };

    private static JsonObject MakeContent(string role, string text) => new()
    {
        ["role"] = role,
        ["parts"] = new JsonArray(new JsonObject { ["text"] = text })
    };

    private async Task<ChatResult> CallGeminiAsync(JsonObject body, CancellationToken cancellationToken)
    {
        var url = ApiBase + Uri.EscapeDataString(_options.Model) + ":generateContent";
        var payload = body.ToJsonString();

        // Los errores temporales de Google (500 y 503) se reintentan hasta 3 veces, con una pausa creciente.
        const int maxAttempts = 3;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                // La clave viaja en un encabezado, no en la URL, para que no quede en registros de URL.
                httpRequest.Headers.Add("x-goog-api-key", _options.ApiKey);

                using var response = await _http.SendAsync(httpRequest, cancellationToken);
                var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
                var statusCode = (int)response.StatusCode;

                if ((statusCode == 500 || statusCode == 503) && attempt < maxAttempts)
                {
                    _logger.LogWarning("Gemini respondió HTTP {StatusCode}. Reintento {Attempt} de {Retries}.",
                        statusCode, attempt, maxAttempts - 1);
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken);
                    continue;
                }

                return ParseResponse(statusCode, responseText);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Gemini no respondió dentro del tiempo límite.");
                return ChatResult.Failure("upstream_error",
                    "Gemini tardó demasiado en responder. Intentá de nuevo en unos segundos.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("Error de red al comunicarse con Gemini: {Message}", ex.Message);
                return ChatResult.Failure("upstream_error",
                    "No se pudo conectar con Gemini. Revisá la conexión a internet e intentá de nuevo.");
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

            var blockReason = root?["promptFeedback"]?["blockReason"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(blockReason))
                return ChatResult.Failure("empty_response",
                    "Gemini no pudo responder a esa consulta. Probá reformularla con otras palabras.");

            var rawText = ExtractText(root);
            if (string.IsNullOrWhiteSpace(rawText))
                return ChatResult.Failure("empty_response",
                    "Gemini respondió, pero la respuesta no se pudo usar. Probá reformular la pregunta.");

            return ParseModelOutput(rawText);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            _logger.LogWarning("No se pudo interpretar la respuesta de Gemini.");
            return ChatResult.Failure("upstream_error",
                "Gemini devolvió una respuesta que no se pudo leer. Intentá de nuevo.");
        }
    }

    private static string ExtractText(JsonNode? root)
    {
        var parts = root?["candidates"]?[0]?["content"]?["parts"] as JsonArray;
        var builder = new StringBuilder();

        if (parts is not null)
        {
            foreach (var part in parts)
            {
                if (part?["thought"]?.GetValue<bool>() == true) continue;
                var text = part?["text"]?.GetValue<string>();
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
            .Trim(' ', '\"', '\'', '¿', '?', '¡', '!', '.', ',', ':', ';', '-', '–', '—');
        if (clean.Length == 0 || clean.Equals("Nuevo chat", StringComparison.OrdinalIgnoreCase)) return null;
        const int maxLength = 54;
        return clean.Length <= maxLength ? clean : clean[..maxLength].TrimEnd();
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
        // Solo registra el mensaje público de Google; nunca la clave, el prompt ni el cuerpo completo.
        var apiMessage = ExtractApiErrorMessage(responseText);
        if (string.IsNullOrEmpty(apiMessage))
            _logger.LogWarning("Gemini respondió con HTTP {StatusCode}.", statusCode);
        else
            _logger.LogWarning("Gemini respondió con HTTP {StatusCode}: {ApiMessage}", statusCode, apiMessage);

        return statusCode switch
        {
            429 => ChatResult.Failure("rate_limited",
                "Se alcanzó temporalmente el límite de solicitudes de Gemini. " +
                "Esperá un momento, revisá tus cuotas en Google AI Studio y volvé a intentar."),
            401 or 403 => ChatResult.Failure("auth_error",
                "Gemini rechazó la clave configurada. Verificá que la clave sea válida " +
                "y tenga acceso a la API de Gemini."),
            400 when responseText.Contains("API key", StringComparison.OrdinalIgnoreCase) =>
                ChatResult.Failure("auth_error",
                    "Gemini no reconoce la clave configurada. Verificá que la clave sea correcta."),
            404 => ChatResult.Failure("model_not_found",
                "El modelo de Gemini configurado no está disponible. Revisá la clave Gemini:Model en appsettings.json."),
            _ => ChatResult.Failure("upstream_error",
                "Gemini no pudo responder en este momento. Intentá de nuevo en unos segundos.")
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
