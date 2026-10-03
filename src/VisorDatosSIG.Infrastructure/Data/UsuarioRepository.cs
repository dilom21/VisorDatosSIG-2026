using System;
using System.Collections.Generic;
using System.Data;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Application.DTOs.Users;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Infrastructure.Data;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public UsuarioRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<UsuarioCredencialesDto?> ObtenerPorLoginAsync(string login, CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);
        const string sql = @"
            SELECT u.IdUsuario, u.Login, u.Nombre, u.PasswordHash, u.PasswordSalt, u.Iteraciones, u.Activo, r.NombreRol
            FROM dbo.Usuarios u
            LEFT JOIN dbo.UsuariosRoles ur ON u.IdUsuario = ur.IdUsuario
            LEFT JOIN dbo.Roles r ON ur.IdRol = r.IdRol
            WHERE u.Login = @Login";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Login", login);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        int idUsuario = 0;
        string userLogin = string.Empty;
        string nombre = string.Empty;
        byte[] passwordHash = Array.Empty<byte>();
        byte[] passwordSalt = Array.Empty<byte>();
        int iteraciones = 0;
        bool activo = false;
        var roles = new List<string>();
        bool found = false;

        while (await reader.ReadAsync(ct))
        {
            if (!found)
            {
                idUsuario = (int)reader["IdUsuario"];
                userLogin = (string)reader["Login"];
                nombre = (string)reader["Nombre"];
                passwordHash = (byte[])reader["PasswordHash"];
                passwordSalt = (byte[])reader["PasswordSalt"];
                iteraciones = (int)reader["Iteraciones"];
                activo = (bool)reader["Activo"];
                found = true;
            }

            if (!reader.IsDBNull(reader.GetOrdinal("NombreRol")))
            {
                roles.Add((string)reader["NombreRol"]);
            }
        }

        if (!found) return null;

        return new UsuarioCredencialesDto
        {
            IdUsuario = idUsuario,
            Login = userLogin,
            Nombre = nombre,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            Iteraciones = iteraciones,
            Activo = activo,
            Roles = roles
        };
    }

    public async Task<bool> CambiarPasswordAsync(int idUsuario, string actual, string nueva, CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);

        const string sqlGet = "SELECT PasswordHash, PasswordSalt, Iteraciones FROM dbo.Usuarios WHERE IdUsuario = @IdUsuario AND Activo = 1";
        using var cmdGet = new SqlCommand(sqlGet, connection);
        cmdGet.Parameters.AddWithValue("@IdUsuario", idUsuario);

        byte[] currentHash;
        byte[] salt;
        int iteraciones;

        using (var reader = await cmdGet.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return false;
            currentHash = (byte[])reader["PasswordHash"];
            salt = (byte[])reader["PasswordSalt"];
            iteraciones = (int)reader["Iteraciones"];
        }

        var inputHash = Rfc2898DeriveBytes.Pbkdf2(actual, salt, iteraciones, HashAlgorithmName.SHA256, 32);
        if (!CryptographicOperations.FixedTimeEquals(inputHash, currentHash))
            return false;

        var newSalt = RandomNumberGenerator.GetBytes(32);
        var newHash = Rfc2898DeriveBytes.Pbkdf2(nueva, newSalt, 100000, HashAlgorithmName.SHA256, 32);

        const string sqlUpdate = @"
            UPDATE dbo.Usuarios
            SET PasswordHash = @PasswordHash, PasswordSalt = @PasswordSalt, Iteraciones = 100000
            WHERE IdUsuario = @IdUsuario AND Activo = 1 AND PasswordHash = @HashAnterior";

        using var cmdUpdate = new SqlCommand(sqlUpdate, connection);
        cmdUpdate.Parameters.AddWithValue("@HashAnterior", currentHash);
        cmdUpdate.Parameters.AddWithValue("@PasswordHash", newHash);
        cmdUpdate.Parameters.AddWithValue("@PasswordSalt", newSalt);
        cmdUpdate.Parameters.AddWithValue("@IdUsuario", idUsuario);

        return await cmdUpdate.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<IEnumerable<UserResponseDto>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        var lista = new List<UserResponseDto>();
        using var connection = await _connectionFactory.AbrirAsync(ct);

        const string sql = @"
            SELECT u.IdUsuario, u.Login, u.Nombre, u.Activo, u.FechaRegistro, r.NombreRol
            FROM dbo.Usuarios u
            LEFT JOIN dbo.UsuariosRoles ur ON u.IdUsuario = ur.IdUsuario
            LEFT JOIN dbo.Roles r ON ur.IdRol = r.IdRol
            ORDER BY u.IdUsuario";

        using var cmd = new SqlCommand(sql, connection);
        using var reader = await cmd.ExecuteReaderAsync(ct);

        var dict = new Dictionary<int, (UserResponseDto dto, List<string> roles)>();

        while (await reader.ReadAsync(ct))
        {
            int id = (int)reader["IdUsuario"];
            if (!dict.TryGetValue(id, out var item))
            {
                var dto = new UserResponseDto(
                    id,
                    (string)reader["Login"],
                    (string)reader["Nombre"],
                    (bool)reader["Activo"],
                    (DateTime)reader["FechaRegistro"],
                    new List<string>()
                );
                item = (dto, new List<string>());
                dict[id] = item;
            }

            if (!reader.IsDBNull(reader.GetOrdinal("NombreRol")))
            {
                item.roles.Add((string)reader["NombreRol"]);
            }
        }

        foreach (var entry in dict.Values)
        {
            foreach (var r in entry.roles)
            {
                entry.dto.Roles.Add(r);
            }
            lista.Add(entry.dto);
        }

        return lista;
    }

    public async Task<UserResponseDto?> ObtenerPorIdAsync(int idUsuario, CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);

        const string sql = @"
            SELECT u.IdUsuario, u.Login, u.Nombre, u.Activo, u.FechaRegistro, r.NombreRol
            FROM dbo.Usuarios u
            LEFT JOIN dbo.UsuariosRoles ur ON u.IdUsuario = ur.IdUsuario
            LEFT JOIN dbo.Roles r ON ur.IdRol = r.IdRol
            WHERE u.IdUsuario = @IdUsuario";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@IdUsuario", idUsuario);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        UserResponseDto? user = null;
        var roles = new List<string>();

        while (await reader.ReadAsync(ct))
        {
            if (user is null)
            {
                user = new UserResponseDto(
                    (int)reader["IdUsuario"],
                    (string)reader["Login"],
                    (string)reader["Nombre"],
                    (bool)reader["Activo"],
                    (DateTime)reader["FechaRegistro"],
                    new List<string>()
                );
            }

            if (!reader.IsDBNull(reader.GetOrdinal("NombreRol")))
            {
                roles.Add((string)reader["NombreRol"]);
            }
        }

        if (user is not null)
        {
            foreach (var r in roles)
            {
                user.Roles.Add(r);
            }
        }

        return user;
    }

    public async Task<UserResponseDto?> CrearUsuarioAsync(CreateUserRequestDto dto, CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);

        const string sqlCheck = "SELECT COUNT(1) FROM dbo.Usuarios WHERE Login = @Login";
        using var cmdCheck = new SqlCommand(sqlCheck, connection);
        cmdCheck.Parameters.AddWithValue("@Login", dto.Login);

        int exists = (int)await cmdCheck.ExecuteScalarAsync(ct);
        if (exists > 0) return null;

        var salt = RandomNumberGenerator.GetBytes(32);
        var hash = Rfc2898DeriveBytes.Pbkdf2(dto.PasswordInicial, salt, 100000, HashAlgorithmName.SHA256, 32);

        using var tx = connection.BeginTransaction();
        try
        {
            const string sqlInsert = @"
                INSERT INTO dbo.Usuarios (Login, Nombre, PasswordHash, PasswordSalt, Iteraciones, Activo, FechaRegistro)
                OUTPUT INSERTED.IdUsuario, INSERTED.FechaRegistro
                VALUES (@Login, @Nombre, @PasswordHash, @PasswordSalt, 100000, 0, SYSUTCDATETIME());";

            using var cmdInsert = new SqlCommand(sqlInsert, connection, tx);
            cmdInsert.Parameters.AddWithValue("@Login", dto.Login);
            cmdInsert.Parameters.AddWithValue("@Nombre", dto.Nombre);
            cmdInsert.Parameters.AddWithValue("@PasswordHash", hash);
            cmdInsert.Parameters.AddWithValue("@PasswordSalt", salt);

            int newId;
            DateTime fechaRegistro;
            using (var reader = await cmdInsert.ExecuteReaderAsync(ct))
            {
                await reader.ReadAsync(ct);
                newId = (int)reader["IdUsuario"];
                fechaRegistro = (DateTime)reader["FechaRegistro"];
            }

            if (dto.Roles is { Count: > 0 })
            {
                foreach (var rol in dto.Roles)
                {
                    const string sqlRol = @"
                        INSERT INTO dbo.UsuariosRoles (IdUsuario, IdRol)
                        SELECT @IdUsuario, IdRol FROM dbo.Roles WHERE NombreRol = @NombreRol";

                    using var cmdRol = new SqlCommand(sqlRol, connection, tx);
                    cmdRol.Parameters.AddWithValue("@IdUsuario", newId);
                    cmdRol.Parameters.AddWithValue("@NombreRol", rol);
                    await cmdRol.ExecuteNonQueryAsync(ct);
                }
            }

            await tx.CommitAsync(ct);
            return new UserResponseDto(newId, dto.Login, dto.Nombre, true, fechaRegistro, dto.Roles ?? new());
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return null;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> CambiarEstadoAsync(int idUsuario, bool activo, CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);

        const string sql = "UPDATE dbo.Usuarios SET Activo = @Activo WHERE IdUsuario = @IdUsuario";
        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Activo", activo);
        cmd.Parameters.AddWithValue("@IdUsuario", idUsuario);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<IReadOnlyList<string>> ObtenerRolesAsync(CancellationToken ct = default)
    {
        var roles = new List<string>();
        using var connection = await _connectionFactory.AbrirAsync(ct);
        using var cmd = new SqlCommand("SELECT NombreRol FROM dbo.Roles ORDER BY NombreRol", connection);
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            roles.Add((string)reader["NombreRol"]);
        }

        return roles;
    }

    public async Task<UserResponseDto?> ActualizarUsuarioAsync(int idUsuario, UpdateUserRequestDto dto, CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);
        var rolesSolicitados = (dto.Roles ?? [])
            .Where(rol => !string.IsNullOrWhiteSpace(rol))
            .Select(rol => rol.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var idsRoles = new List<int>(rolesSolicitados.Length);

        foreach (var rol in rolesSolicitados)
        {
            using var cmdRol = new SqlCommand("SELECT IdRol FROM dbo.Roles WHERE NombreRol = @NombreRol", connection);
            cmdRol.Parameters.Add("@NombreRol", SqlDbType.NVarChar, 50).Value = rol;
            var resultadoRol = await cmdRol.ExecuteScalarAsync(ct);
            if (resultadoRol is null or DBNull)
            {
                return null;
            }

            idsRoles.Add((int)resultadoRol);
        }

        using var tx = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            const string sqlActualizar = "UPDATE dbo.Usuarios SET Nombre = @Nombre WHERE IdUsuario = @IdUsuario";
            using var cmdActualizar = new SqlCommand(sqlActualizar, connection, tx);
            cmdActualizar.Parameters.Add("@Nombre", SqlDbType.NVarChar, 240).Value = dto.Nombre.Trim();
            cmdActualizar.Parameters.Add("@IdUsuario", SqlDbType.Int).Value = idUsuario;

            if (await cmdActualizar.ExecuteNonQueryAsync(ct) == 0)
            {
                await tx.RollbackAsync(ct);
                return null;
            }

            using (var cmdEliminarRoles = new SqlCommand("DELETE FROM dbo.UsuariosRoles WHERE IdUsuario = @IdUsuario", connection, tx))
            {
                cmdEliminarRoles.Parameters.Add("@IdUsuario", SqlDbType.Int).Value = idUsuario;
                await cmdEliminarRoles.ExecuteNonQueryAsync(ct);
            }

            foreach (var idRol in idsRoles)
            {
                using var cmdInsertarRol = new SqlCommand(
                    "INSERT INTO dbo.UsuariosRoles (IdUsuario, IdRol) VALUES (@IdUsuario, @IdRol)", connection, tx);
                cmdInsertarRol.Parameters.Add("@IdUsuario", SqlDbType.Int).Value = idUsuario;
                cmdInsertarRol.Parameters.Add("@IdRol", SqlDbType.Int).Value = idRol;
                await cmdInsertarRol.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        return await ObtenerPorIdAsync(idUsuario, ct);
    }

    public async Task<bool> RestablecerPasswordAsync(int idUsuario, string nuevaPassword, CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);
        var salt = RandomNumberGenerator.GetBytes(32);
        var hash = Rfc2898DeriveBytes.Pbkdf2(nuevaPassword, salt, 100000, HashAlgorithmName.SHA256, 32);

        const string sql = @"
            UPDATE dbo.Usuarios
            SET PasswordHash = @PasswordHash, PasswordSalt = @PasswordSalt, Iteraciones = 100000
            WHERE IdUsuario = @IdUsuario";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.Add("@PasswordHash", SqlDbType.VarBinary, 32).Value = hash;
        cmd.Parameters.Add("@PasswordSalt", SqlDbType.VarBinary, 32).Value = salt;
        cmd.Parameters.Add("@IdUsuario", SqlDbType.Int).Value = idUsuario;

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }
}
