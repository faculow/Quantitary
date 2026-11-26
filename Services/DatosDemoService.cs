using Quantitary.Models;

namespace Quantitary.Services;

public sealed class DatosDemoService
{
    public const string Mail = "demo@quantitary.com";
    public const string Contrasena = "Expo2026";

    private const string NombreEmpresa = "Almacén Horizonte Demo";
    private const string NombreUsuario = "Usuario Demo";
    private const string NombreRol = "Administrador Expo";
    private const string RecursoExcel = "Quantitary.DatosDemo.Productos_Demo_Expo.xlsx";

    private readonly IConfiguration _configuration;

    public DatosDemoService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void GarantizarCuentaYDatos()
    {
        List<Producto> productosExcel = LeerCatalogoIntegrado();
        BD.GarantizarCuentaYDatosDemo(
            _configuration,
            Mail,
            Contrasena,
            NombreEmpresa,
            NombreUsuario,
            NombreRol,
            productosExcel);
    }

    private static List<Producto> LeerCatalogoIntegrado()
    {
        using Stream stream = typeof(DatosDemoService).Assembly.GetManifestResourceStream(RecursoExcel)
            ?? throw new InvalidOperationException("No se encontró el catálogo Excel integrado de la cuenta demo.");

        return ImportacionExcelHelper.LeerProductos(stream);
    }
}
