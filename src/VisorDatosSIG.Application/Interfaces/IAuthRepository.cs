using VisorDatosSIG.Domain.Entities.Security;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato para acceso a datos de usuarios y autenticación (CU01-CU06).
/// </summary>
public interface IAuthRepository
{
    Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<Usuario?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<Usuario?> GetByIdAsync(int idUsuario, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Usuario>> GetAllUsersAsync(CancellationToken cancellationToken = default);
    Task UpdateLastAccessAsync(int idUsuario, CancellationToken cancellationToken = default);
}
