using Microsoft.Data.SqlClient;
using System.Data;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Domain.Entities.Security;
using VisorDatosSIG.Infrastructure.Security;

namespace VisorDatosSIG.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de acceso a datos para usuarios y roles en SQL Server (CU01-CU06).
/// </summary>
public sealed class AuthRepository : IAuthRepository
{
    private readonly SqlServerConnectionFactory _connectionFactory;

    public AuthRepository(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Usuario?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT u.IdUsuario, u.Login, u.Nombre, u.Activo, u.FechaRegistro,
                   r.NombreRol
            FROM dbo.Usuarios u
            LEFT JOIN dbo.UsuariosRoles ur ON ur.IdUsuario = u.IdUsuario
            LEFT JOIN dbo.Roles r ON r.IdRol = ur.IdRol
            WHERE u.Login = @Login;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@Login", SqlDbType.NVarChar, 50) { Value = username });

        Usuario? usuario = null;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            if (usuario is null)
            {
                usuario = new Usuario
                {
                    IdUsuario = reader.GetInt32(0),
                    NombreUsuario = reader.GetString(1),
                    NombreCompleto = reader.GetString(2),
                    Activo = reader.GetBoolean(3),
                    FechaCreacion = reader.GetDateTime(4),
                    Roles = new List<string>()
                };
            }

            if (!reader.IsDBNull(5))
            {
                usuario.Roles.Add(reader.GetString(5));
            }
        }

        return usuario;
    }

    public async Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT PasswordHash, PasswordSalt, Iteraciones, Activo
            FROM dbo.Usuarios
            WHERE Login = @Login;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@Login", SqlDbType.NVarChar, 50) { Value = username });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return false;
        }

        var hash = (byte[])reader.GetValue(0);
        var salt = (byte[])reader.GetValue(1);
        var iteraciones = reader.GetInt32(2);
        var activo = reader.GetBoolean(3);

        if (!activo)
        {
            return false;
        }

        return PasswordHasher.VerifyPassword(password, salt, hash, iteraciones);
    }

    public async Task<Usuario?> GetByIdAsync(int idUsuario, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT u.IdUsuario, u.Login, u.Nombre, u.Activo, u.FechaRegistro,
                   r.NombreRol
            FROM dbo.Usuarios u
            LEFT JOIN dbo.UsuariosRoles ur ON ur.IdUsuario = u.IdUsuario
            LEFT JOIN dbo.Roles r ON r.IdRol = ur.IdRol
            WHERE u.IdUsuario = @Id;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = idUsuario });

        Usuario? usuario = null;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            if (usuario is null)
            {
                usuario = new Usuario
                {
                    IdUsuario = reader.GetInt32(0),
                    NombreUsuario = reader.GetString(1),
                    NombreCompleto = reader.GetString(2),
                    Activo = reader.GetBoolean(3),
                    FechaCreacion = reader.GetDateTime(4),
                    Roles = new List<string>()
                };
            }

            if (!reader.IsDBNull(5))
            {
                usuario.Roles.Add(reader.GetString(5));
            }
        }

        return usuario;
    }

    public async Task<IReadOnlyList<Usuario>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var diccionario = new Dictionary<int, Usuario>();
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT u.IdUsuario, u.Login, u.Nombre, u.Activo, u.FechaRegistro,
                   r.NombreRol
            FROM dbo.Usuarios u
            LEFT JOIN dbo.UsuariosRoles ur ON ur.IdUsuario = u.IdUsuario
            LEFT JOIN dbo.Roles r ON r.IdRol = ur.IdRol
            ORDER BY u.IdUsuario;
            """;

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt32(0);
            if (!diccionario.TryGetValue(id, out var usuario))
            {
                usuario = new Usuario
                {
                    IdUsuario = id,
                    NombreUsuario = reader.GetString(1),
                    NombreCompleto = reader.GetString(2),
                    Activo = reader.GetBoolean(3),
                    FechaCreacion = reader.GetDateTime(4),
                    Roles = new List<string>()
                };
                diccionario[id] = usuario;
            }

            if (!reader.IsDBNull(5))
            {
                usuario.Roles.Add(reader.GetString(5));
            }
        }

        return diccionario.Values.ToList();
    }

    public async Task UpdateLastAccessAsync(int idUsuario, CancellationToken cancellationToken = default)
    {
        // En caso de que se agregue columna UltimoAcceso en la BD
        await Task.CompletedTask;
    }
}
