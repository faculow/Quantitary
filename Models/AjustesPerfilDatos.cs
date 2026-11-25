namespace Quantitary.Models;

public sealed class AjustesPerfilDatos
{
    public string NombreUsuario { get; init; } = string.Empty;
    public string Mail { get; init; } = string.Empty;
    public string? NumeroTelefono { get; init; }
    public string NombreEmpresa { get; init; } = string.Empty;
    public bool PuedeEditarEmpresa { get; init; }
}
