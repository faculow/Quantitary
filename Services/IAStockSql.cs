using Dapper;
using Microsoft.Data.SqlClient;
using Quantitary.Models;

namespace Quantitary.Services;
public sealed class IAStockSql
{
    private readonly IConfiguration _config;
    public IAStockSql(IConfiguration config) => _config = config;
    public async Task<object> Obtener(int usuario, int empresa)
    {
        await using var db = new SqlConnection(BD.ObtenerCadenaConexion(_config));
        var permitido = await db.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM dbo.Usuarios u
          INNER JOIN dbo.Rol r ON r.id=u.rolID
          INNER JOIN dbo.Permisos pe ON pe.id=r.permisoID
          WHERE u.id=@usuario AND u.empresaID=@empresa AND pe.verStock=1", new { usuario, empresa });
        if (permitido == 0) throw new UnauthorizedAccessException();
        var productos = (await db.QueryAsync<AlertaStock>(@"SELECT TOP(100) id AS Id, nombre AS Nombre, sku AS Sku, stockActual AS Stock, stockMinimo AS Minimo
          FROM dbo.Productos WHERE empresaID=@empresa AND stockActual<=stockMinimo
          ORDER BY CASE WHEN stockActual=0 THEN 0 ELSE 1 END, stockActual, nombre", new { empresa })).ToList();
        return new { agotados = productos.Count(x => x.Stock == 0), bajos = productos.Count(x => x.Stock > 0), productos };
    }
    public sealed class AlertaStock { public int Id {get;set;} public string Nombre {get;set;}=""; public string Sku {get;set;}=""; public int Stock {get;set;} public int Minimo {get;set;} }
}
