using Quantitary.Services;

namespace Quantitary.Models;

public sealed record IAOperacionPendiente(
    string Token,
    IAProductoSql.Solicitud Solicitud,
    int Usuario,
    int Empresa,
    int Paso,
    DateTime Vence,
    string Vista);
