using System.Data;
using Microsoft.Data.SqlClient;
using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Infrastructure.Data;

/// <summary>
/// Lectura de usuarios y sus roles desde SQL Server.
/// </summary>
/// <remarks>
/// Todas las consultas son parametrizadas: nunca se concatenan valores proporcionados
/// por el usuario. La contraseña nunca se consulta ni se transporta.
/// </remarks>
public sealed class UsuarioRepository : IUsuarioRepository
{
    private const int LongitudMaximaLogin = 100;

    private const string ConsultaUsuario = """
        SELECT u.IdUsuario,
               u.Login,
               u.Nombre,
               u.PasswordHash,
               u.PasswordSalt,
               u.Iteraciones,
               u.Activo
        FROM dbo.Usuarios AS u
        WHERE u.Login = @Login;
        """;

    private const string ConsultaRoles = """
        SELECT r.NombreRol
        FROM dbo.UsuariosRoles AS ur
        INNER JOIN dbo.Roles AS r ON r.IdRol = ur.IdRol
        WHERE ur.IdUsuario = @IdUsuario
          AND r.Estado = 1
        ORDER BY r.NombreRol;
        """;

    private readonly SqlConnectionFactory _connectionFactory;

    /// <summary>
    /// Inicializa el repositorio con el proveedor de conexiones.
    /// </summary>
    public UsuarioRepository(SqlConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    /// <inheritdoc />
    public async Task<UsuarioCredencialesDto?> ObtenerPorLoginAsync(
        string login,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(login))
        {
            return null;
        }

        var loginNormalizado = login.Trim();
        if (loginNormalizado.Length > LongitudMaximaLogin)
        {
            loginNormalizado = loginNormalizado[..LongitudMaximaLogin];
        }

        await using var conexion = await _connectionFactory.AbrirAsync(cancellationToken);

        int idUsuario;
        string loginAlmacenado;
        string nombre;
        byte[] passwordHash;
        byte[] passwordSalt;
        int iteraciones;
        bool activo;

        await using (var comando = new SqlCommand(ConsultaUsuario, conexion))
        {
            comando.Parameters.Add("@Login", SqlDbType.NVarChar, LongitudMaximaLogin).Value = loginNormalizado;

            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
            if (!await lector.ReadAsync(cancellationToken))
            {
                return null;
            }

            idUsuario = lector.GetInt32(lector.GetOrdinal("IdUsuario"));
            loginAlmacenado = lector.GetString(lector.GetOrdinal("Login"));
            nombre = lector.GetString(lector.GetOrdinal("Nombre"));
            passwordHash = (byte[])lector["PasswordHash"];
            passwordSalt = (byte[])lector["PasswordSalt"];
            iteraciones = lector.GetInt32(lector.GetOrdinal("Iteraciones"));
            activo = lector.GetBoolean(lector.GetOrdinal("Activo"));
        }

        var roles = new List<string>();

        await using (var comando = new SqlCommand(ConsultaRoles, conexion))
        {
            comando.Parameters.Add("@IdUsuario", SqlDbType.Int).Value = idUsuario;

            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                roles.Add(lector.GetString(0));
            }
        }

        return new UsuarioCredencialesDto
        {
            IdUsuario = idUsuario,
            Login = loginAlmacenado,
            Nombre = nombre,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            Iteraciones = iteraciones,
            Activo = activo,
            Roles = roles
        };
    }
}
