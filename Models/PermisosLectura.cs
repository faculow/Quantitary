namespace Quantitary.Models;

public sealed class PermisosLectura
{
    public string NombreEmpresa { get; set; } = string.Empty;
    public bool VerProductos { get; set; }
    public bool VerStock { get; set; }
    public bool VerVentas { get; set; }
    public bool VerGanancias { get; set; }
}
