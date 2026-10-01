using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Application.DTOs.Users;

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
    // Métodos para CU02 (Cambiar Contraseña)
    Task<bool> CambiarPasswordAsync(int idUsuario, string actual, string nueva, CancellationToken ct = default);

    // Métodos para CU03 (Gestionar Usuarios)
    Task<IEnumerable<UserResponseDto>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<UserResponseDto?> ObtenerPorIdAsync(int idUsuario, CancellationToken ct = default);
    Task<UserResponseDto?> CrearUsuarioAsync(CreateUserRequestDto dto, CancellationToken ct = default);
    Task<bool> CambiarEstadoAsync(int idUsuario, bool activo, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ObtenerRolesAsync(CancellationToken ct = default);
    Task<UserResponseDto?> ActualizarUsuarioAsync(int idUsuario, UpdateUserRequestDto dto, CancellationToken ct = default);
    Task<bool> RestablecerPasswordAsync(int idUsuario, string nuevaPassword, CancellationToken ct = default);

}
