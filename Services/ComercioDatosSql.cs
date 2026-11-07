using System.Globalization;
using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;
using Quantitary.Models;

namespace Quantitary.Services;

/// <summary>Contexto real de solo lectura, limitado a la empresa del usuario que inició sesión.</summary>
public sealed class ComercioDatosSql
{
    private readonly IConfiguration _configuration;
    public ComercioDatosSql(IConfiguration configuration) => _configuration = configuration;

    public async Task<string> ConstruirContextoAsync(int usuarioId, int empresaId, CancellationToken ct)
    {
        if (usuarioId <= 0 || empresaId <= 0)
            throw new UnauthorizedAccessException("Sesión inválida.");

        var permisos = await BD.ObtenerPermisosLecturaAsync(_configuration, usuarioId, empresaId);
        if (permisos is null || !(permisos.VerProductos || permisos.VerStock || permisos.VerVentas))
            throw new UnauthorizedAccessException("Tu usuario no tiene permisos para consultar datos desde Quantitary IA.");

        var sb = new StringBuilder();
        sb.AppendLine("DATOS REALES DE QUANTITARY (solo lectura)");
        sb.AppendLine("Fecha: " + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        sb.AppendLine("Comercio: " + Limpiar(permisos.NombreEmpresa));
        sb.AppendLine("Los datos pertenecen exclusivamente a este comercio.");

        if (permisos.VerProductos || permisos.VerStock)
        {
            var resumen = await BD.ObtenerResumenStockAsync(_configuration, empresaId);
            sb.AppendLine($"Cantidad de productos: {resumen.TotalProductos}.");
            if (permisos.VerStock)
                sb.AppendLine($"Agotados: {resumen.Agotados}; con stock bajo: {resumen.BajoMinimo}.");

            var productos = await BD.ObtenerProductosLecturaAsync(_configuration, empresaId, permisos.VerStock);
            sb.AppendLine($"Detalle de hasta 80 productos ({productos.Count} incluidos, priorizando los de bajo stock):");
            foreach (var p in productos)
            {
                var linea = new StringBuilder("- " + Limpiar(p.Nombre));
                if (permisos.VerProductos)
                {
                    linea.Append(" | SKU " + Limpiar(p.Sku) + " | categoría " + Limpiar(p.Categoria));
                    linea.Append(" | precio " + Num(p.Precio));
                    if (permisos.VerGanancias) linea.Append(" | costo " + Num(p.CostoCompra));
                }
                if (permisos.VerStock)
                    linea.Append(" | stock " + p.StockActual + " | mínimo " + p.StockMinimo +
                                 (p.StockActual <= p.StockMinimo ? " | REPONER" : " | OK"));
                sb.AppendLine(linea.ToString());
            }
            if (resumen.TotalProductos > productos.Count)
                sb.AppendLine("Aviso: el detalle no incluye todos los productos; no inventar información sobre productos omitidos.");
        }
        else
            sb.AppendLine("El usuario no tiene permiso para consultar productos ni stock.");

        if (permisos.VerVentas)
        {
            var existeVentas = await BD.ExisteTablaVentasAsync(_configuration);
            if (existeVentas == 1)
            {
                var ventas = await BD.ObtenerVentasUltimos30DiasAsync(_configuration, empresaId);
                sb.AppendLine("Ventas de los últimos 30 días (fecha | operaciones | unidades | total de ventas):");
                foreach (var v in ventas)
                {
                    var linea = v.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) +
                                " | " + v.Operaciones + " | " + v.Unidades + " | " + Num(v.Total);
                    if (permisos.VerGanancias)
                        linea += " | costo " + Num(v.Costo) + " | margen monetario " + Num(v.Total - v.Costo);
                    sb.AppendLine("- " + linea);
                }
                if (ventas.Count == 0) sb.AppendLine("No hay ventas registradas para el período.");

                var ranking = await BD.ObtenerRankingProductosUltimos30DiasAsync(_configuration, empresaId);
                sb.AppendLine("Productos más vendidos de los últimos 30 días (producto | SKU | unidades | facturación):");
                foreach (var p in ranking.Take(15))
                {
                    var linea = $"- {Limpiar(p.Nombre)} | {Limpiar(p.Sku)} | {p.Unidades} | {Num(p.Facturacion)}";
                    if (permisos.VerGanancias) linea += " | ganancia " + Num(p.Ganancia);
                    sb.AppendLine(linea);
                }
                if (ranking.Count == 0) sb.AppendLine("No hay productos vendidos para el período.");
            }
            else
                sb.AppendLine("El historial de ventas todavía no está disponible: falta la tabla Ventas que utiliza el PDV. No inventar datos de ventas.");
        }
        else
            sb.AppendLine("El usuario no tiene permisos para consultar el historial de ventas.");

        sb.AppendLine("No hay datos de plazos de entrega de proveedores: no estimar días de cobertura respecto de entrega sin información.");
        return sb.ToString();
    }

    private static string Limpiar(string? value) =>
        (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/').Trim();
    private static string Num(decimal n) => n.ToString("0.##", CultureInfo.InvariantCulture);
}
