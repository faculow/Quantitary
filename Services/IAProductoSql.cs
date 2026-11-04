using Dapper;
using Microsoft.Data.SqlClient;
using Quantitary.Models;

namespace Quantitary.Services;
public sealed class IAProductoSql
{
    private readonly IConfiguration _config;
    public IAProductoSql(IConfiguration config) => _config=config;
    private SqlConnection Conexion() => new(BD.ObtenerCadenaConexion(_config));
    public sealed record Solicitud(string Tipo, string? Sku, string? Nombre, string? Categoria,
        decimal? Precio, int? Stock, int? StockMinimo, decimal? Costo, string? Proveedor,
        string? Descripcion);
    public sealed class Item { public int Id {get;set;} public string Nombre {get;set;}=""; public string Sku {get;set;}=""; public decimal Precio {get;set;} public int Stock {get;set;} }
    private async Task<bool> Permitido(SqlConnection db,int u,int e,string tipo,SqlTransaction? tx=null)
    {
        var campo=tipo switch {"crear"=>"pe.crearProductos", "precio" or "stock"=>"pe.editarProductos", _=>"0"};
        // El identificador SQL es constante y no contiene datos del cliente.
        return await db.ExecuteScalarAsync<int>($@"SELECT COUNT(*) FROM dbo.Usuarios u
          INNER JOIN dbo.Rol r ON r.id=u.rolID INNER JOIN dbo.Permisos pe ON pe.id=r.permisoID
          WHERE u.id=@u AND u.empresaID=@e AND {campo}=1",new {u,e},tx)==1;
    }
    public async Task<object> VistaPrevia(int u,int e,Solicitud solicitud)
    {
        if (solicitud.Tipo is not ("crear" or "precio" or "stock")) throw new ArgumentException("Acción no habilitada.");
        await using var db=Conexion(); await db.OpenAsync();
        if (!await Permitido(db,u,e,solicitud.Tipo)) throw new UnauthorizedAccessException();
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
            if(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Productos WHERE empresaID=@e AND sku=@sku",new {e,sku})>0)
                throw new ArgumentException("Ya existe ese SKU en tu empresa.");
            return new { tipo="crear", nombre=solicitud.Nombre, sku, precio=solicitud.Precio, stock=solicitud.Stock??0, categoria=solicitud.Categoria??"Sin categoría" };
        }
        if(string.IsNullOrWhiteSpace(solicitud.Sku)) throw new ArgumentException("Escribí el SKU exacto del producto.");
        var coincidencias=(await db.QueryAsync<Item>("SELECT id AS Id,nombre AS Nombre,sku AS Sku,precio AS Precio,stockActual AS Stock FROM dbo.Productos WHERE empresaID=@e AND sku=@sku",new {e,sku=solicitud.Sku.Trim()})).ToList();
        if(coincidencias.Count!=1) throw new ArgumentException("No se encontró un único producto con ese SKU. Revisalo.");
        var item=coincidencias[0];
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
            var duplicado=await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Productos WITH (UPDLOCK, HOLDLOCK) WHERE empresaID=@e AND sku=@sku",new {e,sku=s.Sku.Trim()},tx);
            if(duplicado!=0) throw new InvalidOperationException("El SKU ya existe. No se creó el producto.");
            id=await db.ExecuteScalarAsync<int>(@"INSERT INTO dbo.Productos(empresaID,nombre,sku,categoria,descripcion,fechaCarga,precio,stockActual,stockMinimo,proveedor,costoCompra,precioVenta,cantidadVentas)
              VALUES (@e,@nombre,@sku,@categoria,@descripcion,GETDATE(),@precio,@stock,@minimo,@proveedor,@costo,NULL,0);
              SELECT CONVERT(int,SCOPE_IDENTITY());",new {e,nombre=s.Nombre.Trim(),sku=s.Sku.Trim(),categoria=string.IsNullOrWhiteSpace(s.Categoria)?"Sin categoría":s.Categoria.Trim(),descripcion=s.Descripcion??"",precio=s.Precio.Value,stock=s.Stock??0,minimo=s.StockMinimo??0,proveedor=s.Proveedor??"",costo=s.Costo??0},tx);
            despues=$"Producto: {s.Nombre}; SKU: {s.Sku}; precio: {s.Precio}";
        }
        else
        {
            if(string.IsNullOrWhiteSpace(s.Sku)) throw new ArgumentException("Falta el SKU.");
            var item=await db.QuerySingleOrDefaultAsync<Item>("SELECT id AS Id,nombre AS Nombre,sku AS Sku,precio AS Precio,stockActual AS Stock FROM dbo.Productos WITH (UPDLOCK) WHERE empresaID=@e AND sku=@sku",new {e,sku=s.Sku.Trim()},tx);
            if(item is null) throw new InvalidOperationException("El producto ya no existe. No se aplicó el cambio.");
            id=item.Id;
            if(s.Tipo=="precio")
            {
                if(s.Precio is null or <=0)throw new ArgumentException("Precio inválido.");
                antes=item.Precio.ToString(); despues=s.Precio.Value.ToString();
                await db.ExecuteAsync("UPDATE dbo.Productos SET precio=@precio WHERE id=@id AND empresaID=@e",new {precio=s.Precio.Value,id,e},tx);
            }
            else if(s.Tipo=="stock")
            {
                if(s.Stock is null or <0)throw new ArgumentException("Stock inválido.");
                antes=item.Stock.ToString(); despues=s.Stock.Value.ToString();
                await db.ExecuteAsync("UPDATE dbo.Productos SET stockActual=@stock WHERE id=@id AND empresaID=@e",new {stock=s.Stock.Value,id,e},tx);
            }
            else throw new ArgumentException("Acción no habilitada.");
        }
        await db.ExecuteAsync("INSERT INTO dbo.IAAuditoria(UsuarioId,EmpresaId,Accion,ProductoId,Antes,Despues) VALUES(@u,@e,@accion,@id,@antes,@despues)",new {u,e,accion=s.Tipo,id,antes,despues},tx);
        await tx.CommitAsync();
        return s.Tipo=="crear"?"Producto creado correctamente.":"Cambio guardado correctamente.";
    }
}
