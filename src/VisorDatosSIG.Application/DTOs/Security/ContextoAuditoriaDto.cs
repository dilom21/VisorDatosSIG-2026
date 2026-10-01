namespace VisorDatosSIG.Application.DTOs.Security;

/// <summary>
/// Contexto mínimo de auditoría de una operación: quién la ejecuta y desde dónde.
/// </summary>
/// <remarks>
/// Lo construye el controlador a partir del token JWT y de la conexión HTTP, y lo reciben los
/// servicios que escriben en <c>dbo.Bitacora</c>. Solo contiene datos de auditoría: nunca la
/// contraseña, el hash ni el token.
/// </remarks>
public sealed class ContextoAuditoriaDto
{
    /// <summary>Usuario autenticado que ejecuta la operación; <c>null</c> cuando no se pudo determinar.</summary>
    public int? IdUsuario { get; init; }

    /// <summary>Dirección IP de origen; <c>null</c> cuando no se pudo determinar.</summary>
    public string? Ip { get; init; }
}
