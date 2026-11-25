namespace Quantitary.Models;

public sealed class AjustesViewModel
{
    public PerfilAjustesInput Perfil { get; set; } = new();
    public EmpresaAjustesInput Empresa { get; set; } = new();
    public ContrasenaAjustesInput Seguridad { get; set; } = new();
    public bool PuedeEditarEmpresa { get; set; }
    public string SeccionActiva { get; set; } = "perfil";
}
