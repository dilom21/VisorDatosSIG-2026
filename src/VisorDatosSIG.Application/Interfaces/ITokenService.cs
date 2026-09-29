using VisorDatosSIG.Application.DTOs.Authentication;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato del servicio de generación de tokens JWT.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Genera el token de acceso del usuario autenticado.
    /// </summary>
    /// <param name="usuario">Usuario autenticado (datos públicos y roles).</param>
    /// <returns>Token firmado y su vigencia en segundos.</returns>
    TokenGeneradoDto GenerarToken(AuthenticatedUserDto usuario);
}
