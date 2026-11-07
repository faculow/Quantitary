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
        if (!await BD.IAUsuarioTienePermisoAsync(_config, usuario, empresa, "verStock")) throw new UnauthorizedAccessException();
        var productos = await BD.ObtenerAlertasStockAsync(_config, empresa);
        return new
        {
            agotados = productos.Count(x => x.Stock == 0),
            bajos = productos.Count(x => x.Stock > 0 && x.Stock <= x.Minimo),
            productos
        };
    }
}
