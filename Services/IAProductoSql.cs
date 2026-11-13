using Dapper;
using Microsoft.Data.SqlClient;
using Quantitary.Models;

namespace Quantitary.Services;
public sealed class IAProductoSql
{
    private readonly IConfiguration _config;
    public IAProductoSql(IConfiguration config) => _config = config;
    private SqlConnection Conexion() => new(BD.ObtenerCadenaConexion(_config));
    public sealed record Solicitud(string Tipo, string? Sku, string? Nombre, string? Categoria,
        decimal? Precio, int? Stock, int? StockMinimo, decimal? Costo, string? Proveedor,
        string? Descripcion);
    public sealed class Item { public int Id {get;set;} public string Nombre {get;set;}=""; public string Sku {get;set;}=""; public decimal Precio {get;set;} public int Stock {get;set;} }

    public async Task<List<Item>> BuscarProductos(int u, int e, string? consulta)
    {
        var termino = (consulta ?? "").Trim();
        if (termino.Length is < 2 or > 150) return new List<Item>();
        if (!await BD.IAPermitidoAsync(_config, u, e, "eliminar")) throw new UnauthorizedAccessException();

        var comparador = System.Globalization.CultureInfo.GetCultureInfo("es-AR").CompareInfo;
        var opciones = System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace;
        bool Coincide(string valor) => comparador.IndexOf(valor ?? "", termino, opciones) >= 0;
        int Prioridad(Producto producto)
        {
            if (comparador.Compare(producto.Sku, termino, opciones) == 0) return 0;
            if (comparador.Compare(producto.Nombre, termino, opciones) == 0) return 1;
            if (comparador.IsPrefix(producto.Nombre, termino, opciones)) return 2;
            if (comparador.IsPrefix(producto.Sku, termino, opciones)) return 3;
            return 4;
        }

        return BD.ObtenerProductos(_config, empresaId: e)
            .Where(p => p.Activo && (Coincide(p.Nombre) || Coincide(p.Sku)))
            .OrderBy(Prioridad)
            .ThenBy(p => p.Nombre)
            .Take(8)
            .Select(p => new Item
            {
                Id = p.Id,
                Nombre = p.Nombre,
                Sku = p.Sku,
                Precio = p.PrecioVenta,
                Stock = p.StockActual
            })
            .ToList();
    }
    private async Task<bool> Permitido(SqlConnection db,int u,int e,string tipo,SqlTransaction? tx=null)
        => await BD.IAPermitidoAsync(_config,u,e,tipo);
    public async Task<object> VistaPrevia(int u,int e,Solicitud solicitud)
    {
        if (solicitud.Tipo is not ("crear" or "precio" or "stock" or "eliminar")) throw new ArgumentException("Acción no habilitada.");
        if (!await Permitido(new SqlConnection(BD.ObtenerCadenaConexion(_config)),u,e,solicitud.Tipo)) throw new UnauthorizedAccessException();
        if(solicitud.Tipo=="crear")
        {
            if(string.IsNullOrWhiteSpace(solicitud.Nombre)||solicitud.Nombre.Trim().Length>150) throw new ArgumentException("Indicá un nombre válido.");
            if((solicitud.Categoria?.Length??0)>100 || (solicitud.Proveedor?.Length??0)>150 || (solicitud.Descripcion?.Length??0)>500)
                throw new ArgumentException("Hay campos demasiado largos.");
            if(solicitud.Precio is <= 0 or > 9999999999 || solicitud.Stock is < 0 || solicitud.StockMinimo is < 0 || solicitud.Costo is < 0 or > 9999999999)
                throw new ArgumentException("El precio debe ser mayor a cero y los demás valores no pueden ser negativos.");
            if(solicitud.Precio is null) throw new ArgumentException("El precio es obligatorio para guardar en esta versión. Podés omitir otros campos.");
            var sku=(solicitud.Sku??"").Trim();
            if(sku.Length is < 2 or > 100) throw new ArgumentException("Indicá un SKU de entre 2 y 100 caracteres.");
            if(await BD.IAProductoExisteSkuAsync(_config,e,sku))
                throw new ArgumentException("Ya existe ese SKU en tu empresa.");
            return new { tipo="crear", nombre=solicitud.Nombre, sku, precio=solicitud.Precio, stock=solicitud.Stock??0, categoria=solicitud.Categoria??"Sin categoría" };
        }
        if(string.IsNullOrWhiteSpace(solicitud.Sku)) throw new ArgumentException("Escribí el SKU exacto del producto.");
        var coincidencias=(await BD.IABuscarProductoPorSkuAsync(_config,e,solicitud.Sku.Trim())).ToList();
        if(coincidencias.Count!=1) throw new ArgumentException("No se encontró un único producto con ese SKU. Revisalo.");
        var item=coincidencias[0];
        if(solicitud.Tipo=="eliminar")
        {
            var ventas=await BD.IAContarVentasProductoAsync(_config,e,item.Id);
            if(ventas>0) throw new ArgumentException("Este producto tiene ventas registradas y todavía no puede eliminarse sin romper el historial. No se modificó nada.");
            return new { tipo="eliminar", producto=item.Nombre, sku=item.Sku, precio=item.Precio, stock=item.Stock, advertencia="Esta acción elimina el producto definitivamente." };
        }
        if(solicitud.Tipo=="precio" && (solicitud.Precio is null or <=0 or >9999999999))throw new ArgumentException("Indicá un precio válido.");
        if(solicitud.Tipo=="stock" && (solicitud.Stock is null or <0))throw new ArgumentException("Indicá un stock válido.");
        return new { tipo=solicitud.Tipo, producto=item.Nombre, sku=item.Sku, anterior=solicitud.Tipo=="precio"?(object)item.Precio:item.Stock, nuevo=solicitud.Tipo=="precio"?(object)solicitud.Precio!:solicitud.Stock! };
    }
    public async Task<string> Ejecutar(int u,int e,Solicitud s)
    {
        await using var db=Conexion(); await db.OpenAsync();
        await using var tx=(SqlTransaction)await db.BeginTransactionAsync();
        if(!await Permitido(db,u,e,s.Tipo,tx)) throw new UnauthorizedAccessException();
        int id; string antes="";string despues="";
        if(s.Tipo=="crear")
        {
            if(string.IsNullOrWhiteSpace(s.Nombre) || string.IsNullOrWhiteSpace(s.Sku) || s.Precio is null or <=0)
                throw new ArgumentException("Faltan datos obligatorios.");
            var duplicado=await BD.IAProductoDuplicadoSkuAsync(_config,e,s.Sku.Trim(),tx);
            if(duplicado!=0) throw new InvalidOperationException("El SKU ya existe. No se creó el producto.");
            id=await BD.IACrearProductoAsync(_config,e,s.Nombre,s.Sku,s.Categoria,s.Descripcion,s.Precio.Value,s.Stock??0,s.StockMinimo??0,s.Proveedor,s.Costo??0,tx);
            despues=$"Producto: {s.Nombre}; SKU: {s.Sku}; precio: {s.Precio}";
        }
        else
        {
            if(string.IsNullOrWhiteSpace(s.Sku)) throw new ArgumentException("Falta el SKU.");
            var items=await BD.IABuscarProductoPorSkuAsync(_config,e,s.Sku.Trim(),tx);
            if(items.Count != 1) throw new InvalidOperationException("El producto no existe o hay múltiples coincidencias. No se aplicó el cambio.");
            var item=items[0];
            id=item.Id;
            if(s.Tipo=="eliminar")
            {
                var ventas=await BD.IAContarVentasProductoAsync(_config,e,id,tx);
                if(ventas>0) throw new InvalidOperationException("El producto tiene ventas registradas y no puede eliminarse. No se modificó nada.");
                antes=$"Producto: {item.Nombre}; SKU: {item.Sku}; precio: {item.Precio}; stock: {item.Stock}";
                despues="Producto eliminado";
                var eliminado=await BD.IAEliminarProductoAsync(_config,e,id,tx);
                if(!eliminado) throw new InvalidOperationException("No se pudo eliminar el producto.");
            }
            else if(s.Tipo=="precio")
            {
                if(s.Precio is null or <=0)throw new ArgumentException("Precio inválido.");
                antes=item.Precio.ToString(); despues=s.Precio.Value.ToString();
                var cambio=await BD.IAActualizarProductoPrecioAsync(_config,e,id,s.Precio.Value,tx);
                if(!cambio) throw new InvalidOperationException("No se pudo actualizar el precio.");
            }
            else if(s.Tipo=="stock")
            {
                if(s.Stock is null or <0)throw new ArgumentException("Stock inválido.");
                antes=item.Stock.ToString(); despues=s.Stock.Value.ToString();
                var cambio=await BD.IAActualizarProductoStockAsync(_config,e,id,s.Stock.Value,tx);
                if(!cambio) throw new InvalidOperationException("No se pudo actualizar el stock.");
            }
            else throw new ArgumentException("Acción no habilitada.");
        }
        await BD.IAInsertarAuditoriaAsync(_config,u,e,s.Tipo,id,antes,despues,tx);
        await tx.CommitAsync();
        return s.Tipo switch
        {
            "crear" => "Producto creado correctamente.",
            "eliminar" => "Producto eliminado correctamente.",
            _ => "Cambio guardado correctamente."
        };
    }
}
