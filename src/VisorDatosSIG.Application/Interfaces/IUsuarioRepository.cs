using VisorDatosSIG.Application.DTOs.Authentication;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato de acceso a los usuarios y sus roles almacenados en SQL Server.
/// </summary>
public interface IUsuarioRepository
{
    /// <summary>
    /// Obtiene las credenciales y los roles activos del usuario indicado.
    /// </summary>
    /// <param name="login">Login del usuario.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Credenciales del usuario o <c>null</c> cuando no existe.</returns>
    Task<UsuarioCredencialesDto?> ObtenerPorLoginAsync(string login, CancellationToken cancellationToken = default);
}
