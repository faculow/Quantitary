using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Quantitary.Models;

namespace Quantitary.Services;

/// <summary>Resuelve saludos, navegación y consultas simples sin consumir tokens del proveedor.</summary>
public sealed class IARespuestaLocalSql
{
    private readonly IConfiguration _configuration;
    public IARespuestaLocalSql(IConfiguration configuration) => _configuration = configuration;

    public async Task<ChatResult?> Resolver(int usuario, int empresa, ChatRequest request)
    {
        if (request.Image is not null) return null;
        var original = request.Message?.Trim() ?? string.Empty;
        var text = Normalizar(original);
        if (text.Length == 0) return null;

        if (EsSaludo(text))
            return Result("¡Hola! ¿En qué te puedo ayudar con Quantitary o con tu negocio?", "Saludo");

        if (EsAgradecimiento(text))
            return Result("¡De nada! Cuando quieras, seguimos con tu negocio.", "Consulta");

        if (Contiene(text, "que podes hacer", "como me podes ayudar", "para que servis"))
            return Result(
                "Puedo consultar stock, productos y ventas; mostrar tablas y gráficos; analizar el negocio; y ayudarte a crear, modificar o eliminar productos con confirmación.",
                "Ayuda de Quantitary IA",
                new ChatAction("Ver stock", "/Stock/Index"), new ChatAction("Ir al PDV", "/Pdv/Index"), new ChatAction("Ver análisis", "/Home/AnalisisVentasGanancias"));

        if (Contiene(text, "como crear un producto", "como agrego un producto", "donde creo un producto", "donde agrego un producto"))
            return Result("Podés cargarlo desde Stock > Agregar producto. Si querés que lo prepare desde el chat, escribí “creá un producto” y voy a abrir el formulario seguro.",
                "Crear producto", new ChatAction("Agregar producto", "/Stock/Crear"));

        var skuMatch = Regex.Match(original, @"\bSKU\s*[:#-]?\s*([A-Za-z0-9._-]+)", RegexOptions.IgnoreCase);
        if (skuMatch.Success && Contiene(text, "stock", "precio", "producto"))
        {
            var wantsStock = Contiene(text, "stock");
            var wantsPrice = Contiene(text, "precio");
            if (wantsStock && !await BD.IAUsuarioTienePermisoAsync(_configuration, usuario, empresa, "verStock")) throw new UnauthorizedAccessException();
            if ((!wantsStock || wantsPrice) && !await BD.IAUsuarioTienePermisoAsync(_configuration, usuario, empresa, "verProductos")) throw new UnauthorizedAccessException();
            var sku = skuMatch.Groups[1].Value;
            var items = await BD.IABuscarProductoPorSkuAsync(_configuration, empresa, sku);
            if (items.Count != 1)
                return Result("No encontré un único producto con ese SKU. Revisalo e intentá nuevamente.", "Consulta de producto",
                    new ChatAction("Buscar en Stock", "/Stock/Index?sku=" + Uri.EscapeDataString(sku)));
            var item = items[0];
            var detail = wantsStock && wantsPrice
                ? $"tiene {item.Stock} unidades y un precio de {item.Precio:0.##}"
                : wantsStock ? $"tiene {item.Stock} unidades" : wantsPrice ? $"tiene un precio de {item.Precio:0.##}" : "está registrado en el catálogo";
            return Result($"{item.Nombre} (SKU {item.Sku}) {detail}.",
                "Consulta de producto", new ChatAction("Abrir en Stock", "/Stock/Index?sku=" + Uri.EscapeDataString(item.Sku)));
        }

        if (PideTablaStock(text))
        {
            if (!await BD.IAUsuarioTienePermisoAsync(_configuration, usuario, empresa, "verStock"))
                throw new UnauthorizedAccessException();
            var productos = await BD.ObtenerAlertasStockAsync(_configuration, empresa);
            var soloBajo = Contiene(text, "stock bajo", "poco stock", "agotados", "agotado", "reponer");
            var seleccion = (soloBajo ? productos.Where(p => p.Stock <= p.Minimo) : productos).Take(50).ToList();
            var rows = seleccion.Select(p => new List<string>
            {
                p.Nombre, p.Sku, p.Stock.ToString(CultureInfo.InvariantCulture),
                p.Minimo.ToString(CultureInfo.InvariantCulture),
                p.Stock == 0 ? "Agotado" : p.Stock <= p.Minimo ? "Stock bajo" : "Disponible"
            }).ToList();
            var table = new ChatTable(soloBajo ? "Productos para reponer" : "Stock actual",
                new List<string> { "Producto", "SKU", "Stock", "Mínimo", "Estado" }, rows);
            var reply = seleccion.Count == 0
                ? "No encontré productos para mostrar con ese filtro."
                : $"Encontré {seleccion.Count} producto{(seleccion.Count == 1 ? "" : "s")}. Podés administrarlos desde Stock.";
            return ChatResult.Success(reply, new List<ChatTable> { table }, new List<ChatChart>(), "Consulta de stock",
                request.Section?.Equals("stock", StringComparison.OrdinalIgnoreCase) == true
                    ? new List<ChatAction>() : new List<ChatAction> { new ChatAction("Administrar stock", "/Stock/Index") });
        }

        if (PideRankingProductos(text))
        {
            if (!await BD.IAUsuarioTienePermisoAsync(_configuration, usuario, empresa, "verVentas"))
                throw new UnauthorizedAccessException();
            var limitaA30Dias = Contiene(text, "ultimos 30 dias", "ultimo mes", "este mes", "mes pasado");
            var ranking = await BD.ObtenerRankingProductosAsync(_configuration, empresa, limitaA30Dias ? 30 : null);
            var numero = Regex.Match(text, @"\b(\d{1,2})\b");
            var limite = numero.Success && int.TryParse(numero.Groups[1].Value, out var solicitado)
                ? Math.Clamp(solicitado, 1, 20)
                : 5;
            var periodo = limitaA30Dias ? "en los últimos 30 días" : "en todo el historial";
            if (ranking.Count == 0)
                return Result($"No encontré productos vendidos {periodo} para esta empresa.", "Productos más vendidos",
                    new ChatAction("Ver historial", "/Home/Historial"));
            var seleccion = ranking.Take(limite).ToList();
            var rows = seleccion.Select((p, i) => new List<string>
            {
                (i + 1).ToString(CultureInfo.InvariantCulture), p.Nombre, p.Sku,
                p.Unidades.ToString(CultureInfo.InvariantCulture), p.Facturacion.ToString("0.##", CultureInfo.InvariantCulture)
            }).ToList();
            var top = seleccion[0];
            return ChatResult.Success(
                $"El producto más vendido {periodo} fue {top.Nombre}, con {top.Unidades} unidades. Te muestro los primeros {seleccion.Count}.",
                new List<ChatTable> { new ChatTable($"Productos más vendidos · {periodo}", new List<string> { "#", "Producto", "SKU", "Unidades", "Facturación" }, rows) },
                new List<ChatChart>(), "Productos más vendidos", new List<ChatAction> { new ChatAction("Ver análisis", "/Home/AnalisisProducto"), new ChatAction("Ver historial", "/Home/Historial") });
        }

        if (PideNavegacion(text, "stock"))
            return Result("Podés consultar y administrar tus productos desde el apartado Stock.", "Stock", new ChatAction("Ir a Stock", "/Stock/Index"));
        if (PideNavegacion(text, "pdv") || Contiene(text, "hacer una venta", "registrar una venta", "quiero vender"))
            return Result("Podés registrar la operación desde el Punto de Venta.", "Punto de venta", new ChatAction("Ir al PDV", "/Pdv/Index"));
        if (PideNavegacion(text, "historial"))
            return Result("En el historial podés consultar y filtrar las ventas registradas.", "Historial", new ChatAction("Ver historial", "/Home/Historial"));
        if (PideNavegacion(text, "ajustes") || Contiene(text, "configurar mi empresa", "cambiar mi contrasena"))
            return Result("Los datos del perfil, la empresa y la seguridad se administran desde Ajustes.", "Ajustes", new ChatAction("Ir a Ajustes", "/Ajustes/Index"));

        return null;
    }

    private static ChatResult Result(string reply, string title, params ChatAction[] actions) =>
        ChatResult.Success(reply, new(), new(), title, actions.ToList());

    private static bool EsSaludo(string text) =>
        text is "hola" or "buenas" or "buen dia" or "buenas tardes" or "buenas noches" or "hey" or "holaa";
    private static bool EsAgradecimiento(string text) =>
        text is "gracias" or "muchas gracias" or "genial gracias" or "perfecto gracias";
    private static bool PideTablaStock(string text) =>
        (Contiene(text, "tabla") && Contiene(text, "stock", "productos", "inventario")) ||
        Contiene(text, "productos con stock bajo", "productos agotados", "que productos tengo que reponer", "lista de stock");
    private static bool PideRankingProductos(string text) =>
        Contiene(text, "ranking de productos", "producto mas vendido", "productos mas vendidos") ||
        (Contiene(text, "producto", "productos") &&
         Contiene(text, "que mas vendi", "mas vendi", "vendi mas", "mas se vendio", "mas se vendieron", "mas ventas"));
    private static bool PideNavegacion(string text, string destino) =>
        Contiene(text, destino) && Contiene(text, "ir", "abrir", "entrar", "donde", "apartado", "seccion", "ver mi", "mostrar", "mostrame", "consultar");
    private static bool Contiene(string value, params string[] fragments) => fragments.Any(value.Contains);
    private static string Normalizar(string value)
    {
        var source = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(source.Length);
        foreach (var c in source)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(c));
        return builder.ToString().Normalize(NormalizationForm.FormC).Trim(' ', '.', ',', '!', '¡', '?', '¿');
    }
}
