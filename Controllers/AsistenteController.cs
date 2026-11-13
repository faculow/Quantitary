using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;
using Quantitary.Services;

namespace Quantitary.Controllers;

public sealed class AsistenteController : Controller
{
    private readonly ComercioDatosSql _datos;
    private readonly IAiChatService _ai;
    private readonly ILogger<AsistenteController> _logger;
    private readonly IAHistorialSql _historial;
    private readonly IAStockSql _stock;
    private readonly IAProductoSql _producto;
    private readonly IARespuestaLocalSql _local;

    public AsistenteController(ComercioDatosSql datos, IAiChatService ai, IAHistorialSql historial, IAStockSql stock, IAProductoSql producto, IARespuestaLocalSql local, ILogger<AsistenteController> logger)
    {
        _datos = datos;
        _ai = ai;
        _historial = historial;
        _stock = stock;
        _producto = producto;
        _local = local;
        _logger = logger;
    }

    [HttpPost("/api/chat")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("quantitary-ia")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest? request, CancellationToken cancellationToken)
    {
        var usuarioId = HttpContext.Session.GetInt32("UsuarioID");
        var empresaId = HttpContext.Session.GetInt32("EmpresaID");
        if (usuarioId is not > 0 || empresaId is not > 0)
            return Unauthorized(ChatResult.Failure("unauthorized", "Iniciá sesión para usar Quantitary IA."));

        if (request is null || (string.IsNullOrWhiteSpace(request.Message) && request.Image is null) ||
            (request.Message?.Length ?? 0) > AiChatLimits.MaxMessageLength)
            return BadRequest(ChatResult.Failure("invalid_request", "Escribí una pregunta o adjuntá una imagen válida."));

        if (!ChatImageValidator.TryValidate(request.Image, out var safeImage, out var imageError))
            return BadRequest(ChatResult.Failure("invalid_image", imageError!));

        var message = string.IsNullOrWhiteSpace(request.Message)
            ? "Analizá esta imagen y explicame la información útil para mi negocio."
            : request.Message.Trim();
        request = request with { Message = message, Image = safeImage };

        try
        {
            if (request.ChatId is not Guid chatId || !await _historial.Existe(chatId, usuarioId.Value, empresaId.Value))
                return BadRequest(ChatResult.Failure("invalid_chat", "Elegí un chat válido antes de enviar."));

            var local = await _local.Resolver(usuarioId.Value, empresaId.Value, request);
            if (local is not null)
            {
                await _historial.GuardarIntercambio(chatId, usuarioId.Value, empresaId.Value, message, local);
                return Ok(local);
            }

            var historico = await _historial.Leer(chatId, usuarioId.Value, empresaId.Value);
            var contexto = await _datos.ConstruirContextoAsync(usuarioId.Value, empresaId.Value, cancellationToken);
            contexto = $"Pantalla actual: {LimpiarRuta(request.Section)} / {LimpiarRuta(request.Action)}.\n" + contexto;
            var seguro = request with { History = historico.TakeLast(AiChatLimits.MaxHistoryTurns).ToList() };
            var resultado = await _ai.SendAsync(seguro, contexto, cancellationToken);
            if (resultado.Ok && resultado.Reply is not null)
            {
                var preguntaGuardada = safeImage is null ? message : $"{message} [Imagen adjunta: {safeImage.FileName}]";
                await _historial.GuardarIntercambio(chatId, usuarioId.Value, empresaId.Value, preguntaGuardada, resultado);
            }
            return StatusCode(resultado.Error?.Code switch
            {
                null => 200,
                "invalid_request" => 400,
                "rate_limited" => 429,
                "config_missing" => 503,
                _ => 502
            }, resultado);
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, ChatResult.Failure("forbidden", "No tenés permisos para consultar estos datos."));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consultar el contexto comercial para Quantitary IA.");
            return StatusCode(503, ChatResult.Failure("data_unavailable",
                "No se pudieron consultar los datos del comercio. Verificá la base de datos."));
        }
    }

    private static string LimpiarRuta(string? value)
    {
        var clean = new string((value ?? "desconocida").Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return clean.Length == 0 ? "desconocida" : clean[..Math.Min(clean.Length, 40)];
    }

    private (int Usuario, int Empresa)? Sesion()
    {
        var usuario = HttpContext.Session.GetInt32("UsuarioID") ?? 0;
        var empresa = HttpContext.Session.GetInt32("EmpresaID") ?? 0;
        return usuario > 0 && empresa > 0 ? (usuario, empresa) : null;
    }

    [HttpGet("/api/ia/chats")]
    public async Task<IActionResult> Chats()
    {
        var s = Sesion(); if (s is null) return Unauthorized();
        if (!await _historial.UsuarioValido(s.Value.Usuario, s.Value.Empresa)) return Forbid();
        return Ok(await _historial.Listar(s.Value.Usuario, s.Value.Empresa));
    }

    [HttpPost("/api/ia/chats")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NuevoChat()
    {
        var s = Sesion(); if (s is null) return Unauthorized();
        if (!await _historial.UsuarioValido(s.Value.Usuario, s.Value.Empresa)) return Forbid();
        return Ok(new { id = await _historial.Crear(s.Value.Usuario, s.Value.Empresa) });
    }

    [HttpGet("/api/ia/chats/{chatId:guid}")]
    public async Task<IActionResult> Mensajes(Guid chatId)
    {
        var s = Sesion(); if (s is null) return Unauthorized();
        if (!await _historial.Existe(chatId, s.Value.Usuario, s.Value.Empresa)) return NotFound();
        return Ok(await _historial.Leer(chatId, s.Value.Usuario, s.Value.Empresa));
    }

    public sealed record RenombrarRequest(string? Titulo);

    [HttpPatch("/api/ia/chats/{chatId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Renombrar(Guid chatId, [FromBody] RenombrarRequest request)
    {
        var s = Sesion(); if (s is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Titulo) || request.Titulo.Length > 120) return BadRequest();
        return await _historial.Renombrar(chatId, s.Value.Usuario, s.Value.Empresa, request.Titulo) ? Ok() : NotFound();
    }

    [HttpPost("/api/ia/chats/{chatId:guid}/renombrar")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RenombrarPost(Guid chatId, [FromBody] RenombrarRequest request) => Renombrar(chatId, request);

    [HttpDelete("/api/ia/chats/{chatId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(Guid chatId)
    {
        var s = Sesion(); if (s is null) return Unauthorized();
        return await _historial.Eliminar(chatId, s.Value.Usuario, s.Value.Empresa) ? Ok() : NotFound();
    }

    [HttpPost("/api/ia/chats/{chatId:guid}/eliminar")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> EliminarPost(Guid chatId) => Eliminar(chatId);

    [HttpGet("/api/ia/alertas-stock")]
    public async Task<IActionResult> AlertasStock()
    {
        var s = Sesion(); if (s is null) return Unauthorized();
        try { return Ok(await _stock.Obtener(s.Value.Usuario, s.Value.Empresa)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("/api/ia/productos/buscar")]
    public async Task<IActionResult> BuscarProducto([FromQuery] string? q)
    {
        var s = Sesion(); if (s is null) return Unauthorized();
        try { return Ok(new { productos = await _producto.BuscarProductos(s.Value.Usuario, s.Value.Empresa, q) }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    public sealed record ConfirmacionRequest(string? Token);
    private sealed record Pendiente(string Token, IAProductoSql.Solicitud Solicitud, int Usuario, int Empresa, int Paso, DateTime Vence, string Vista);

    [HttpPost("/api/ia/productos/preparar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PrepararProducto([FromBody] IAProductoSql.Solicitud solicitud)
    {
        var s=Sesion(); if(s is null)return Unauthorized();
        try
        {
            var resumen=await _producto.VistaPrevia(s.Value.Usuario,s.Value.Empresa,solicitud);
            var token=Guid.NewGuid().ToString("N");
            HttpContext.Session.SetString("IAOperacion",JsonSerializer.Serialize(new Pendiente(token,solicitud,s.Value.Usuario,s.Value.Empresa,0,DateTime.UtcNow.AddMinutes(5),JsonSerializer.Serialize(resumen))));
            return Ok(new { token, resumen, mensaje="Primera confirmación: revisá los datos antes de continuar." });
        }
        catch(UnauthorizedAccessException) {return Forbid();}
        catch(ArgumentException ex){return BadRequest(new {error=ex.Message});}
    }

    [HttpPost("/api/ia/productos/confirmar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmarProducto([FromBody] ConfirmacionRequest request)
    {
        var s=Sesion();if(s is null)return Unauthorized();
        var raw=HttpContext.Session.GetString("IAOperacion");
        if(raw is null)return BadRequest(new {error="No hay operaciones pendientes."});
        Pendiente? op;
        try{op=JsonSerializer.Deserialize<Pendiente>(raw);}catch{return BadRequest();}
        if(op is null||op.Token!=request.Token||op.Usuario!=s.Value.Usuario||op.Empresa!=s.Value.Empresa||op.Vence<DateTime.UtcNow)
        {HttpContext.Session.Remove("IAOperacion");return BadRequest(new {error="Operación vencida. Volvé a prepararla."});}
        if(op.Paso==0)
        {
            HttpContext.Session.SetString("IAOperacion",JsonSerializer.Serialize(op with {Paso=1}));
            return Ok(new {confirmado=false,mensaje="Segunda confirmación: ¿ejecutar este cambio definitivamente?"});
        }
        // Token de un solo uso. Se consume antes de realizar el cambio.
        HttpContext.Session.Remove("IAOperacion");
        try
        {
            // Revalidación de los datos y los permisos inmediatamente antes de escribir.
            var actual = await _producto.VistaPrevia(s.Value.Usuario,s.Value.Empresa,op.Solicitud);
            if (JsonSerializer.Serialize(actual) != op.Vista)
                return Conflict(new {error="El producto cambió desde la vista previa. Prepará de nuevo la operación."});
            var mensaje=await _producto.Ejecutar(s.Value.Usuario,s.Value.Empresa,op.Solicitud);
            return Ok(new {confirmado=true,mensaje});
        }
        catch(UnauthorizedAccessException){return Forbid();}
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException)
        {return BadRequest(new {error=ex.Message});}
    }

    [HttpPost("/api/ia/productos/cancelar")]
    [ValidateAntiForgeryToken]
    public IActionResult CancelarProducto()
    {
        if(Sesion() is null)return Unauthorized();
        HttpContext.Session.Remove("IAOperacion");
        return Ok();
    }
}
