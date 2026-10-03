using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using VisorDatosSIG.Application.DTOs.Empleados;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Infrastructure.Data;

/// <summary>
/// Implementación ADO.NET con consultas parametrizadas para el acceso a datos de empleados (CU04).
/// </summary>
public sealed class EmpleadoRepository : IEmpleadoRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public EmpleadoRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IEnumerable<EmpleadoResponseDto>> ObtenerTodosAsync(
        string? busqueda = null,
        string? cargo = null,
        string? disponibilidad = null,
        bool? activo = null,
        CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);

        var sqlBuilder = new StringBuilder(@"
            SELECT IdEmpleado, Codigo, Nombres, Apellidos, DocumentoIdentidad,
                   Telefono, Email, Cargo, Area, Disponibilidad, Activo,
                   FechaRegistro, FechaModificacion, Observaciones
            FROM dbo.Empleados
            WHERE 1 = 1");

        using var cmd = new SqlCommand();
        cmd.Connection = connection;

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            sqlBuilder.Append(@" AND (
                Codigo LIKE @Busqueda OR
                Nombres LIKE @Busqueda OR
                Apellidos LIKE @Busqueda OR
                DocumentoIdentidad LIKE @Busqueda)");
            cmd.Parameters.AddWithValue("@Busqueda", $"%{busqueda.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(cargo))
        {
            sqlBuilder.Append(" AND Cargo = @Cargo");
            cmd.Parameters.AddWithValue("@Cargo", cargo.Trim());
        }

        if (!string.IsNullOrWhiteSpace(disponibilidad))
        {
            sqlBuilder.Append(" AND Disponibilidad = @Disponibilidad");
            cmd.Parameters.AddWithValue("@Disponibilidad", disponibilidad.Trim());
        }

        if (activo.HasValue)
        {
            sqlBuilder.Append(" AND Activo = @Activo");
            cmd.Parameters.AddWithValue("@Activo", activo.Value);
        }

        sqlBuilder.Append(" ORDER BY Activo DESC, Apellidos ASC, Nombres ASC");
        cmd.CommandText = sqlBuilder.ToString();

        using var reader = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<EmpleadoResponseDto>();

        while (await reader.ReadAsync(ct))
        {
            lista.Add(MapearEmpleado(reader));
        }

        return lista;
    }

    public async Task<EmpleadoResponseDto?> ObtenerPorIdAsync(int idEmpleado, CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);
        const string sql = @"
            SELECT IdEmpleado, Codigo, Nombres, Apellidos, DocumentoIdentidad,
                   Telefono, Email, Cargo, Area, Disponibilidad, Activo,
                   FechaRegistro, FechaModificacion, Observaciones
            FROM dbo.Empleados
            WHERE IdEmpleado = @IdEmpleado";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@IdEmpleado", idEmpleado);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return MapearEmpleado(reader);
        }

        return null;
    }

    public async Task<EmpleadoResponseDto?> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);
        const string sql = @"
            SELECT IdEmpleado, Codigo, Nombres, Apellidos, DocumentoIdentidad,
                   Telefono, Email, Cargo, Area, Disponibilidad, Activo,
                   FechaRegistro, FechaModificacion, Observaciones
            FROM dbo.Empleados
            WHERE Codigo = @Codigo";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Codigo", codigo.Trim());

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return MapearEmpleado(reader);
        }

        return null;
    }

    public async Task<EmpleadoResponseDto?> CrearAsync(CreateEmpleadoRequestDto dto, CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);

        const string sql = @"
            INSERT INTO dbo.Empleados (
                Codigo, Nombres, Apellidos, DocumentoIdentidad,
                Telefono, Email, Cargo, Area, Disponibilidad,
                Activo, FechaRegistro, Observaciones
            )
            OUTPUT inserted.IdEmpleado, inserted.FechaRegistro
            VALUES (
                @Codigo, @Nombres, @Apellidos, @DocumentoIdentidad,
                @Telefono, @Email, @Cargo, @Area, @Disponibilidad,
                1, SYSDATETIME(), @Observaciones
            );";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Codigo", dto.Codigo.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("@Nombres", dto.Nombres.Trim());
        cmd.Parameters.AddWithValue("@Apellidos", dto.Apellidos.Trim());
        cmd.Parameters.AddWithValue("@DocumentoIdentidad", dto.DocumentoIdentidad.Trim());
        cmd.Parameters.AddWithValue("@Telefono", (object?)dto.Telefono?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Email", (object?)dto.Email?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Cargo", dto.Cargo.Trim());
        cmd.Parameters.AddWithValue("@Area", dto.Area.Trim());
        cmd.Parameters.AddWithValue("@Disponibilidad", dto.Disponibilidad.Trim());
        cmd.Parameters.AddWithValue("@Observaciones", (object?)dto.Observaciones?.Trim() ?? DBNull.Value);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            int id = (int)reader["IdEmpleado"];
            DateTime fechaRegistro = (DateTime)reader["FechaRegistro"];

            return new EmpleadoResponseDto(
                id,
                dto.Codigo.Trim().ToUpperInvariant(),
                dto.Nombres.Trim(),
                dto.Apellidos.Trim(),
                $"{dto.Nombres.Trim()} {dto.Apellidos.Trim()}",
                dto.DocumentoIdentidad.Trim(),
                dto.Telefono?.Trim(),
                dto.Email?.Trim(),
                dto.Cargo.Trim(),
                dto.Area.Trim(),
                dto.Disponibilidad.Trim(),
                true,
                fechaRegistro,
                null,
                dto.Observaciones?.Trim());
        }

        return null;
    }

    public async Task<EmpleadoResponseDto?> ActualizarAsync(int idEmpleado, UpdateEmpleadoRequestDto dto, CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);

        const string sql = @"
            UPDATE dbo.Empleados
            SET Nombres = @Nombres,
                Apellidos = @Apellidos,
                DocumentoIdentidad = @DocumentoIdentidad,
                Telefono = @Telefono,
                Email = @Email,
                Cargo = @Cargo,
                Area = @Area,
                Disponibilidad = @Disponibilidad,
                Observaciones = @Observaciones,
                FechaModificacion = SYSDATETIME()
            OUTPUT inserted.IdEmpleado, inserted.Codigo, inserted.Nombres, inserted.Apellidos,
                   inserted.DocumentoIdentidad, inserted.Telefono, inserted.Email, inserted.Cargo,
                   inserted.Area, inserted.Disponibilidad, inserted.Activo, inserted.FechaRegistro,
                   inserted.FechaModificacion, inserted.Observaciones
            WHERE IdEmpleado = @IdEmpleado;";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@IdEmpleado", idEmpleado);
        cmd.Parameters.AddWithValue("@Nombres", dto.Nombres.Trim());
        cmd.Parameters.AddWithValue("@Apellidos", dto.Apellidos.Trim());
        cmd.Parameters.AddWithValue("@DocumentoIdentidad", dto.DocumentoIdentidad.Trim());
        cmd.Parameters.AddWithValue("@Telefono", (object?)dto.Telefono?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Email", (object?)dto.Email?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Cargo", dto.Cargo.Trim());
        cmd.Parameters.AddWithValue("@Area", dto.Area.Trim());
        cmd.Parameters.AddWithValue("@Disponibilidad", dto.Disponibilidad.Trim());
        cmd.Parameters.AddWithValue("@Observaciones", (object?)dto.Observaciones?.Trim() ?? DBNull.Value);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return MapearEmpleado(reader);
        }

        return null;
    }

    public async Task<bool> CambiarEstadoAsync(int idEmpleado, bool activo, CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);
        const string sql = @"
            UPDATE dbo.Empleados
            SET Activo = @Activo,
                FechaModificacion = SYSDATETIME()
            WHERE IdEmpleado = @IdEmpleado";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@IdEmpleado", idEmpleado);
        cmd.Parameters.AddWithValue("@Activo", activo);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<bool> CambiarDisponibilidadAsync(int idEmpleado, string disponibilidad, string? motivo = null, CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);

        var sqlBuilder = new StringBuilder(@"
            UPDATE dbo.Empleados
            SET Disponibilidad = @Disponibilidad,
                FechaModificacion = SYSDATETIME()");

        if (!string.IsNullOrWhiteSpace(motivo))
        {
            sqlBuilder.Append(@",
                Observaciones = CASE
                    WHEN Observaciones IS NULL OR Observaciones = '' THEN @Motivo
                    ELSE CONCAT(Observaciones, ' | ', @Motivo)
                END");
        }

        sqlBuilder.Append(" WHERE IdEmpleado = @IdEmpleado");

        using var cmd = new SqlCommand(sqlBuilder.ToString(), connection);
        cmd.Parameters.AddWithValue("@IdEmpleado", idEmpleado);
        cmd.Parameters.AddWithValue("@Disponibilidad", disponibilidad.Trim());

        if (!string.IsNullOrWhiteSpace(motivo))
        {
            cmd.Parameters.AddWithValue("@Motivo", $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm}]: {motivo.Trim()}");
        }

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<bool> ExisteCodigoAsync(string codigo, int? ignorarId = null, CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);
        var sql = "SELECT COUNT(1) FROM dbo.Empleados WHERE Codigo = @Codigo";
        if (ignorarId.HasValue)
        {
            sql += " AND IdEmpleado <> @IgnorarId";
        }

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Codigo", codigo.Trim());
        if (ignorarId.HasValue)
        {
            cmd.Parameters.AddWithValue("@IgnorarId", ignorarId.Value);
        }

        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        return count > 0;
    }

    public async Task<bool> ExisteDocumentoAsync(string documento, int? ignorarId = null, CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);
        var sql = "SELECT COUNT(1) FROM dbo.Empleados WHERE DocumentoIdentidad = @Documento";
        if (ignorarId.HasValue)
        {
            sql += " AND IdEmpleado <> @IgnorarId";
        }

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Documento", documento.Trim());
        if (ignorarId.HasValue)
        {
            cmd.Parameters.AddWithValue("@IgnorarId", ignorarId.Value);
        }

        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        return count > 0;
    }

    public async Task<IReadOnlyList<string>> ObtenerCargosAsync(CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);
        const string sql = @"
            SELECT DISTINCT Cargo
            FROM dbo.Empleados
            WHERE Cargo IS NOT NULL AND Cargo <> ''
            ORDER BY Cargo ASC";

        using var cmd = new SqlCommand(sql, connection);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<string>();

        while (await reader.ReadAsync(ct))
        {
            lista.Add((string)reader["Cargo"]);
        }

        // Si no hay ninguno aún en base de datos, proveer catálogo por defecto
        if (lista.Count == 0)
        {
            lista.AddRange([
                "Técnico de Campo",
                "Supervisor de Zona",
                "Conductor de Cuadrilla",
                "Inspectora Catastral",
                "Operador de Redes"
            ]);
        }

        return lista;
    }

    public async Task<IReadOnlyList<string>> ObtenerDisponibilidadesAsync(CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);
        const string sql = @"
            SELECT DISTINCT Disponibilidad
            FROM dbo.Empleados
            WHERE Disponibilidad IS NOT NULL AND Disponibilidad <> ''
            ORDER BY Disponibilidad ASC";

        using var cmd = new SqlCommand(sql, connection);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<string>();

        while (await reader.ReadAsync(ct))
        {
            lista.Add((string)reader["Disponibilidad"]);
        }

        var baseDisponibilidades = new List<string> { "Disponible", "En Servicio", "Baja Parcial", "Licencia", "Vacaciones" };
        foreach (var item in baseDisponibilidades)
        {
            if (!lista.Contains(item, StringComparer.OrdinalIgnoreCase))
            {
                lista.Add(item);
            }
        }

        return lista;
    }

    public async Task<EmpleadoMetricasDto> ObtenerMetricasAsync(CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);
        const string sql = @"
            SELECT
                COUNT(1) AS Total,
                SUM(CASE WHEN Activo = 1 THEN 1 ELSE 0 END) AS Activos,
                SUM(CASE WHEN Activo = 0 THEN 1 ELSE 0 END) AS Inactivos,
                SUM(CASE WHEN Activo = 1 AND Disponibilidad = 'Disponible' THEN 1 ELSE 0 END) AS Disponibles,
                SUM(CASE WHEN Activo = 1 AND Disponibilidad = 'En Servicio' THEN 1 ELSE 0 END) AS EnServicio,
                SUM(CASE WHEN Activo = 1 AND Disponibilidad = 'Baja Parcial' THEN 1 ELSE 0 END) AS BajasParciales,
                SUM(CASE WHEN Activo = 1 AND Disponibilidad NOT IN ('Disponible', 'En Servicio', 'Baja Parcial') THEN 1 ELSE 0 END) AS Otros
            FROM dbo.Empleados";

        using var cmd = new SqlCommand(sql, connection);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new EmpleadoMetricasDto(
                reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader[0]),
                reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader[1]),
                reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader[2]),
                reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader[3]),
                reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader[4]),
                reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader[5]),
                reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader[6]));
        }

        return new EmpleadoMetricasDto(0, 0, 0, 0, 0, 0, 0);
    }

    private static EmpleadoResponseDto MapearEmpleado(SqlDataReader reader)
    {
        string nombres = (string)reader["Nombres"];
        string apellidos = (string)reader["Apellidos"];

        return new EmpleadoResponseDto(
            (int)reader["IdEmpleado"],
            (string)reader["Codigo"],
            nombres,
            apellidos,
            $"{nombres} {apellidos}".Trim(),
            (string)reader["DocumentoIdentidad"],
            reader.IsDBNull(reader.GetOrdinal("Telefono")) ? null : (string)reader["Telefono"],
            reader.IsDBNull(reader.GetOrdinal("Email")) ? null : (string)reader["Email"],
            (string)reader["Cargo"],
            (string)reader["Area"],
            (string)reader["Disponibilidad"],
            (bool)reader["Activo"],
            (DateTime)reader["FechaRegistro"],
            reader.IsDBNull(reader.GetOrdinal("FechaModificacion")) ? null : (DateTime?)reader["FechaModificacion"],
            reader.IsDBNull(reader.GetOrdinal("Observaciones")) ? null : (string)reader["Observaciones"]);
    }
}
