namespace Quantitary.Models;

public sealed class PerfilAjustesDatos
{
    public string NombreUsuario { get; set; } = string.Empty;
    public string Mail { get; set; } = string.Empty;
    public string? NumeroTelefono { get; set; }
    public string NombreEmpresa { get; set; } = string.Empty;
    public bool PuedeEditarEmpresa { get; set; }
}
