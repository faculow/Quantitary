namespace Quantitary.Models;

public sealed class IAChatResumen
{
    public Guid Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public DateTime ActualizadoEn { get; set; }
}
