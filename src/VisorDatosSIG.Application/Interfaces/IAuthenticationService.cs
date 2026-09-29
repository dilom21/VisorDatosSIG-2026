using VisorDatosSIG.Application.DTOs.Authentication;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato del servicio de autenticación.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Valida las credenciales del usuario, genera el token de acceso y registra el intento
    /// en la bitácora (exitoso o fallido).
    /// </summary>
    /// <param name="solicitud">Login y contraseña recibidos.</param>
    /// <param name="ipOrigen">IP de origen de la petición, cuando está disponible.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resultado del intento, sin exponer datos internos.</returns>
    Task<LoginResultDto> IniciarSesionAsync(
        LoginRequestDto solicitud,
        string? ipOrigen = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra el cierre de sesión del usuario autenticado.
    /// </summary>
    /// <param name="idUsuario">Identificador tomado del JWT.</param>
    /// <param name="login">Login tomado del JWT.</param>
    /// <param name="ipOrigen">IP de origen de la petición, cuando está disponible.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns><c>true</c> cuando el cierre se registró correctamente.</returns>
    Task<bool> CerrarSesionAsync(
        int idUsuario,
        string? login,
        string? ipOrigen = null,
        CancellationToken cancellationToken = default);
}
