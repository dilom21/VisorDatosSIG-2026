using Microsoft.Data.SqlClient;
using System.Data;
using VisorDatosSIG.Application.DTOs.BajasParciales;
using VisorDatosSIG.Application.Exceptions;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Infrastructure.Data;

/// <summary>
/// Implementación ADO.NET para CU18 - Gestionar Bajas Parciales.
/// </summary>
public sealed class BajaParcialRepository : IBajaParcialRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public BajaParcialRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IReadOnlyList<BajaParcialEmpleadoCatalogoDto>> ObtenerEmpleadosCatalogoAsync(
        CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);
        const string sql = @"
            SELECT IdEmpleado, Codigo,
                   CONCAT(Nombres, ' ', Apellidos) AS NombreCompleto,
                   Disponibilidad, Activo
            FROM dbo.Empleados
            WHERE Activo = 1
            ORDER BY Apellidos ASC, Nombres ASC, Codigo ASC;";

        using var command = new SqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync(ct);
        var empleados = new List<BajaParcialEmpleadoCatalogoDto>();

        while (await reader.ReadAsync(ct))
        {
            empleados.Add(new BajaParcialEmpleadoCatalogoDto(
                (int)reader["IdEmpleado"],
                (string)reader["Codigo"],
                (string)reader["NombreCompleto"],
                (string)reader["Disponibilidad"],
                (bool)reader["Activo"]));
        }

        return empleados;
    }

    public async Task<IEnumerable<BajaParcialResponseDto>> ObtenerTodosAsync(
        int? idEmpleado = null,
        string? estado = null,
        CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);

        var sql = @"
            SELECT
                b.IdBajaParcial,
                b.IdEmpleado,
                e.Codigo AS CodigoEmpleado,
                CONCAT(e.Nombres, ' ', e.Apellidos) AS NombreEmpleado,
                b.FechaInicio,
                b.FechaFin,
                b.Motivo,
                b.Observaciones,
                b.Estado,
                b.FechaRegistro,
                b.FechaModificacion
            FROM dbo.BajasParciales b
            INNER JOIN dbo.Empleados e
                ON e.IdEmpleado = b.IdEmpleado
            WHERE 1 = 1";

        using var cmd = new SqlCommand();
        cmd.Connection = connection;

        if (idEmpleado.HasValue)
        {
            sql += " AND b.IdEmpleado = @IdEmpleado";
            cmd.Parameters.AddWithValue("@IdEmpleado", idEmpleado.Value);
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            sql += " AND b.Estado = @Estado";
            cmd.Parameters.AddWithValue("@Estado", estado.Trim());
        }

        sql += " ORDER BY b.FechaInicio DESC, b.IdBajaParcial DESC";

        cmd.CommandText = sql;

        using var reader = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<BajaParcialResponseDto>();

        while (await reader.ReadAsync(ct))
        {
            lista.Add(Mapear(reader));
        }

        return lista;
    }

    public async Task<BajaParcialResponseDto?> ObtenerPorIdAsync(
        int idBajaParcial,
        CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);

        const string sql = @"
            SELECT
                b.IdBajaParcial,
                b.IdEmpleado,
                e.Codigo AS CodigoEmpleado,
                CONCAT(e.Nombres, ' ', e.Apellidos) AS NombreEmpleado,
                b.FechaInicio,
                b.FechaFin,
                b.Motivo,
                b.Observaciones,
                b.Estado,
                b.FechaRegistro,
                b.FechaModificacion
            FROM dbo.BajasParciales b
            INNER JOIN dbo.Empleados e
                ON e.IdEmpleado = b.IdEmpleado
            WHERE b.IdBajaParcial = @IdBajaParcial;";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@IdBajaParcial", idBajaParcial);

        using var reader = await cmd.ExecuteReaderAsync(ct);

        return await reader.ReadAsync(ct)
            ? Mapear(reader)
            : null;
    }

    public async Task<BajaParcialResponseDto?> CrearAsync(
        CreateBajaParcialRequestDto dto,
        CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);

        try
        {
            await AdquirirBloqueoEmpleadoAsync(connection, transaction, dto.IdEmpleado, ct);

            const string sqlEmpleadoActual = @"
                SELECT Disponibilidad
                FROM dbo.Empleados WITH (UPDLOCK, HOLDLOCK)
                WHERE IdEmpleado = @IdEmpleado
                  AND Activo = 1;";

            using var cmdEmpleadoActual = new SqlCommand(
                sqlEmpleadoActual, connection, transaction);
            cmdEmpleadoActual.Parameters.Add("@IdEmpleado", SqlDbType.Int).Value = dto.IdEmpleado;

            var disponibilidadActual =
                await cmdEmpleadoActual.ExecuteScalarAsync(ct);

            if (disponibilidadActual is null || disponibilidadActual == DBNull.Value)
            {
                await transaction.RollbackAsync(ct);
                return null;
            }

            if (await ExisteSolapamientoEnTransaccionAsync(
                    connection, transaction, dto.IdEmpleado,
                    dto.FechaInicio, dto.FechaFin, null, ct))
            {
                throw new BajaParcialSolapamientoException();
            }

            var disponibilidadAnterior = Convert.ToString(disponibilidadActual);
        const string sqlInsert = @"
            INSERT INTO dbo.BajasParciales
            (
                IdEmpleado,
                FechaInicio,
                FechaFin,
                Motivo,
                Observaciones,
                Estado,
                FechaRegistro,
                DisponibilidadAnterior
            )
            OUTPUT inserted.IdBajaParcial
            VALUES
            (
                @IdEmpleado,
                @FechaInicio,
                @FechaFin,
                @Motivo,
                @Observaciones,
                N'Activa',
                SYSDATETIME(),
                @DisponibilidadAnterior
            );";

        using var cmdInsert =
            new SqlCommand(sqlInsert, connection, transaction);

        cmdInsert.Parameters.Add("@IdEmpleado", SqlDbType.Int).Value = dto.IdEmpleado;
        cmdInsert.Parameters.Add("@FechaInicio", SqlDbType.Date).Value = dto.FechaInicio.Date;
        cmdInsert.Parameters.Add("@FechaFin", SqlDbType.Date).Value = dto.FechaFin.Date;
        cmdInsert.Parameters.Add("@Motivo", SqlDbType.NVarChar, 300).Value = dto.Motivo.Trim();
        cmdInsert.Parameters.Add("@Observaciones", SqlDbType.NVarChar, 500).Value =
            (object?)dto.Observaciones?.Trim() ?? DBNull.Value;
        cmdInsert.Parameters.Add("@DisponibilidadAnterior", SqlDbType.NVarChar, 50).Value =
            (object?)disponibilidadAnterior ?? DBNull.Value;

        var resultado = await cmdInsert.ExecuteScalarAsync(ct);

        if (resultado is null)
        {
            await transaction.RollbackAsync(ct);
            return null;
        }

        var hoy = DateTime.Today;

        if (dto.FechaInicio.Date <= hoy &&
            dto.FechaFin.Date >= hoy)
        {
            const string sqlEmpleado = @"
                UPDATE dbo.Empleados
                SET Disponibilidad = N'Baja Parcial',
                    FechaModificacion = SYSDATETIME()
                WHERE IdEmpleado = @IdEmpleado
                  AND Activo = 1;";

            using var cmdEmpleado =
                new SqlCommand(sqlEmpleado, connection, transaction);

            cmdEmpleado.Parameters.Add("@IdEmpleado", SqlDbType.Int).Value = dto.IdEmpleado;

            await cmdEmpleado.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);

        return await ObtenerPorIdAsync(
            Convert.ToInt32(resultado),
            ct);
    }
    catch
    {
        await transaction.RollbackAsync(ct);
        throw;
    }
}

    public async Task<BajaParcialResponseDto?> ActualizarAsync(
        int idBajaParcial,
        UpdateBajaParcialRequestDto dto,
        CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);

        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);

        try
        {
            const string sqlActual = @"
                SELECT IdEmpleado, Estado, DisponibilidadAnterior
                FROM dbo.BajasParciales WITH (UPDLOCK, HOLDLOCK)
                WHERE IdBajaParcial = @IdBajaParcial;";

            int idEmpleado;
            string estado;
            string? disponibilidadAnterior;

            using (var cmdActual = new SqlCommand(sqlActual, connection, transaction))
            {
                cmdActual.Parameters.Add("@IdBajaParcial", SqlDbType.Int).Value = idBajaParcial;
                using var reader = await cmdActual.ExecuteReaderAsync(ct);

                if (!await reader.ReadAsync(ct))
                {
                    await transaction.RollbackAsync(ct);
                    return null;
                }

                idEmpleado = (int)reader["IdEmpleado"];
                estado = (string)reader["Estado"];
                disponibilidadAnterior = reader["DisponibilidadAnterior"] == DBNull.Value
                    ? null
                    : (string)reader["DisponibilidadAnterior"];
            }

            await AdquirirBloqueoEmpleadoAsync(connection, transaction, idEmpleado, ct);

            if (!estado.Equals("Cancelada", StringComparison.OrdinalIgnoreCase) &&
                await ExisteSolapamientoEnTransaccionAsync(
                    connection, transaction, idEmpleado,
                    dto.FechaInicio, dto.FechaFin, idBajaParcial, ct))
            {
                throw new BajaParcialSolapamientoException();
            }

            const string sql = @"
                UPDATE dbo.BajasParciales
                SET FechaInicio = @FechaInicio,
                    FechaFin = @FechaFin,
                    Motivo = @Motivo,
                    Observaciones = @Observaciones,
                    FechaModificacion = SYSDATETIME()
                WHERE IdBajaParcial = @IdBajaParcial;";

            using (var cmd = new SqlCommand(sql, connection, transaction))
            {
                cmd.Parameters.Add("@IdBajaParcial", SqlDbType.Int).Value = idBajaParcial;
                cmd.Parameters.Add("@FechaInicio", SqlDbType.Date).Value = dto.FechaInicio.Date;
                cmd.Parameters.Add("@FechaFin", SqlDbType.Date).Value = dto.FechaFin.Date;
                cmd.Parameters.Add("@Motivo", SqlDbType.NVarChar, 300).Value = dto.Motivo.Trim();
                cmd.Parameters.Add("@Observaciones", SqlDbType.NVarChar, 500).Value =
                    (object?)dto.Observaciones?.Trim() ?? DBNull.Value;

                await cmd.ExecuteNonQueryAsync(ct);
            }

            await SincronizarEmpleadoEnTransaccionAsync(
                connection, transaction, idEmpleado, DateTime.Today, ct);

            await transaction.CommitAsync(ct);
            return await ObtenerPorIdAsync(idBajaParcial, ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> CambiarEstadoAsync(
        int idBajaParcial,
        string estado,
        CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);

        try
        {
        const string sqlBaja = @"
            SELECT
                IdEmpleado,
                FechaInicio,
                FechaFin,
                Estado AS EstadoActual,
                DisponibilidadAnterior
            FROM dbo.BajasParciales WITH (UPDLOCK, HOLDLOCK)
            WHERE IdBajaParcial = @IdBajaParcial;";

        int idEmpleado;
        DateTime fechaInicio;
        DateTime fechaFin;
        string estadoActual;
        string? disponibilidadAnterior;

        using (var cmdBaja =
            new SqlCommand(sqlBaja, connection, transaction))
        {
            cmdBaja.Parameters.AddWithValue(
                "@IdBajaParcial",
                idBajaParcial);

            using var reader = await cmdBaja.ExecuteReaderAsync(ct);

            if (!await reader.ReadAsync(ct))
            {
                await transaction.RollbackAsync(ct);
                return false;
            }

            idEmpleado = (int)reader["IdEmpleado"];
            fechaInicio = (DateTime)reader["FechaInicio"];
            fechaFin = (DateTime)reader["FechaFin"];
            estadoActual = (string)reader["EstadoActual"];

            disponibilidadAnterior =
                reader["DisponibilidadAnterior"] == DBNull.Value
                    ? null
                    : (string)reader["DisponibilidadAnterior"];
        }

        await AdquirirBloqueoEmpleadoAsync(connection, transaction, idEmpleado, ct);

        if (estado.Equals("Activa", StringComparison.OrdinalIgnoreCase) &&
            !estadoActual.Equals("Activa", StringComparison.OrdinalIgnoreCase) &&
            await ExisteSolapamientoEnTransaccionAsync(
                connection, transaction, idEmpleado,
                fechaInicio, fechaFin, idBajaParcial, ct))
        {
            throw new BajaParcialSolapamientoException();
        }

        const string sqlEstado = @"
            UPDATE dbo.BajasParciales
            SET Estado = @Estado,
                FechaModificacion = SYSDATETIME()
            WHERE IdBajaParcial = @IdBajaParcial;";

        using (var cmdEstado =
            new SqlCommand(sqlEstado, connection, transaction))
        {
            cmdEstado.Parameters.AddWithValue(
                "@IdBajaParcial",
                idBajaParcial);

            cmdEstado.Parameters.AddWithValue(
                "@Estado",
                estado.Trim());

            var filas = await cmdEstado.ExecuteNonQueryAsync(ct);

            if (filas == 0)
            {
                await transaction.RollbackAsync(ct);
                return false;
            }
        }

        await SincronizarEmpleadoEnTransaccionAsync(
            connection, transaction, idEmpleado, DateTime.Today, ct);

        await transaction.CommitAsync(ct);
        return true;
    }
    catch
    {
        await transaction.RollbackAsync(ct);
        throw;
    }
    }

    public async Task<bool> ExisteSolapamientoAsync(
        int idEmpleado,
        DateTime fechaInicio,
        DateTime fechaFin,
        int? ignorarIdBajaParcial = null,
        CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.AbrirAsync(ct);

        var sql = @"
            SELECT COUNT(1)
            FROM dbo.BajasParciales
            WHERE IdEmpleado = @IdEmpleado
              AND Estado <> N'Cancelada'
              AND FechaInicio <= @FechaFin
              AND FechaFin >= @FechaInicio";

        if (ignorarIdBajaParcial.HasValue)
        {
            sql += " AND IdBajaParcial <> @IgnorarIdBajaParcial";
        }

        using var cmd = new SqlCommand(sql, connection);

        cmd.Parameters.AddWithValue("@IdEmpleado", idEmpleado);
        cmd.Parameters.AddWithValue("@FechaInicio", fechaInicio.Date);
        cmd.Parameters.AddWithValue("@FechaFin", fechaFin.Date);

        if (ignorarIdBajaParcial.HasValue)
        {
            cmd.Parameters.AddWithValue(
                "@IgnorarIdBajaParcial",
                ignorarIdBajaParcial.Value);
        }

        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
    }


    public async Task<int> SincronizarVigenciasAsync(
    DateTime fechaReferencia,
    CancellationToken ct = default)
{
    using var connection = await _connectionFactory.AbrirAsync(ct);
    using var transaction = connection.BeginTransaction();

    try
    {
        var fecha = fechaReferencia.Date;

        // -------------------------------------------------------------
        // 1. Finalizar automáticamente periodos que ya vencieron.
        //    Se conserva qué empleados fueron afectados para restaurar
        //    su disponibilidad anterior.
        // -------------------------------------------------------------
        const string sqlFinalizar = @"
            DECLARE @Finalizadas TABLE
            (
                IdBajaParcial INT,
                IdEmpleado INT,
                DisponibilidadAnterior NVARCHAR(50),
                FechaFin DATE
            );

            UPDATE dbo.BajasParciales
            SET Estado = N'Finalizada',
                FechaModificacion = SYSDATETIME()
            OUTPUT
                inserted.IdBajaParcial,
                inserted.IdEmpleado,
                inserted.DisponibilidadAnterior,
                inserted.FechaFin
            INTO @Finalizadas
            WHERE Estado = N'Activa'
              AND FechaFin < @FechaReferencia;

            DECLARE @CantidadFinalizadas INT = @@ROWCOUNT;

            ;WITH UltimaFinalizada AS
            (
                SELECT
                    IdEmpleado,
                    DisponibilidadAnterior,
                    ROW_NUMBER() OVER
                    (
                        PARTITION BY IdEmpleado
                        ORDER BY FechaFin DESC, IdBajaParcial DESC
                    ) AS rn
                FROM @Finalizadas
            )
            UPDATE e
            SET
                e.Disponibilidad =
                    CASE
                        WHEN NULLIF(
                            LTRIM(RTRIM(f.DisponibilidadAnterior)),
                            N''
                        ) IS NULL
                            THEN N'Disponible'
                        ELSE f.DisponibilidadAnterior
                    END,
                e.FechaModificacion = SYSDATETIME()
            FROM dbo.Empleados e
            INNER JOIN UltimaFinalizada f
                ON f.IdEmpleado = e.IdEmpleado
               AND f.rn = 1
            WHERE e.Activo = 1
              AND e.Disponibilidad = N'Baja Parcial'
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.BajasParciales vigente
                  WHERE vigente.IdEmpleado = e.IdEmpleado
                    AND vigente.Estado = N'Activa'
                    AND vigente.FechaInicio <= @FechaReferencia
                    AND vigente.FechaFin >= @FechaReferencia
              );

            SELECT @CantidadFinalizadas;";

        int finalizadas;

        using (var cmdFinalizar =
            new SqlCommand(
                sqlFinalizar,
                connection,
                transaction))
        {
            cmdFinalizar.Parameters.AddWithValue(
                "@FechaReferencia",
                fecha);

            finalizadas = Convert.ToInt32(
                await cmdFinalizar.ExecuteScalarAsync(ct));
        }

        // -------------------------------------------------------------
        // 2. Para las bajas que comienzan hoy o ya están vigentes,
        //    conservar la disponibilidad previa.
        //
        //    También corrige el caso de una baja futura creada mientras
        //    el empleado estaba en otra Baja Parcial.
        // -------------------------------------------------------------
        const string sqlDisponibilidadAnterior = @"
            UPDATE b
            SET b.DisponibilidadAnterior = e.Disponibilidad
            FROM dbo.BajasParciales b
            INNER JOIN dbo.Empleados e
                ON e.IdEmpleado = b.IdEmpleado
            WHERE b.Estado = N'Activa'
              AND b.FechaInicio <= @FechaReferencia
              AND b.FechaFin >= @FechaReferencia
              AND e.Activo = 1
              AND e.Disponibilidad <> N'Baja Parcial'
              AND
              (
                  b.DisponibilidadAnterior IS NULL
                  OR LTRIM(RTRIM(b.DisponibilidadAnterior)) = N''
                  OR b.DisponibilidadAnterior = N'Baja Parcial'
              );";

        using (var cmdAnterior =
            new SqlCommand(
                sqlDisponibilidadAnterior,
                connection,
                transaction))
        {
            cmdAnterior.Parameters.AddWithValue(
                "@FechaReferencia",
                fecha);

            await cmdAnterior.ExecuteNonQueryAsync(ct);
        }

        // -------------------------------------------------------------
        // 3. Aplicar Baja Parcial a todo empleado que tenga un periodo
        //    activo y vigente para la fecha indicada.
        // -------------------------------------------------------------
        const string sqlActivar = @"
            UPDATE e
            SET
                e.Disponibilidad = N'Baja Parcial',
                e.FechaModificacion = SYSDATETIME()
            FROM dbo.Empleados e
            WHERE e.Activo = 1
              AND e.Disponibilidad <> N'Baja Parcial'
              AND EXISTS
              (
                  SELECT 1
                  FROM dbo.BajasParciales b
                  WHERE b.IdEmpleado = e.IdEmpleado
                    AND b.Estado = N'Activa'
                    AND b.FechaInicio <= @FechaReferencia
                    AND b.FechaFin >= @FechaReferencia
              );";

        int activadas;

        using (var cmdActivar =
            new SqlCommand(
                sqlActivar,
                connection,
                transaction))
        {
            cmdActivar.Parameters.AddWithValue(
                "@FechaReferencia",
                fecha);

            activadas = await cmdActivar.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);

        return finalizadas + activadas;
    }
    catch
    {
        await transaction.RollbackAsync(ct);
        throw;
    }
}
    private static async Task AdquirirBloqueoEmpleadoAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int idEmpleado,
        CancellationToken ct)
    {
        using var command = new SqlCommand(
            @"DECLARE @resultado INT;
              EXEC @resultado = sys.sp_getapplock
                  @Resource = @recurso,
                  @LockMode = 'Exclusive',
                  @LockOwner = 'Transaction',
                  @LockTimeout = 10000;
              IF @resultado < 0 THROW 50001, 'No fue posible adquirir el bloqueo de bajas parciales.', 1;",
            connection,
            transaction);
        command.Parameters.Add("@recurso", SqlDbType.NVarChar, 255).Value =
            $"BajasParciales:Empleado:{idEmpleado}";
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<bool> ExisteSolapamientoEnTransaccionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int idEmpleado,
        DateTime fechaInicio,
        DateTime fechaFin,
        int? ignorarIdBajaParcial,
        CancellationToken ct)
    {
        var sql = @"
            SELECT COUNT(1)
            FROM dbo.BajasParciales WITH (UPDLOCK, HOLDLOCK)
            WHERE IdEmpleado = @IdEmpleado
              AND Estado <> N'Cancelada'
              AND FechaInicio <= @FechaFin
              AND FechaFin >= @FechaInicio";

        if (ignorarIdBajaParcial.HasValue)
        {
            sql += " AND IdBajaParcial <> @IgnorarIdBajaParcial";
        }

        using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@IdEmpleado", SqlDbType.Int).Value = idEmpleado;
        command.Parameters.Add("@FechaInicio", SqlDbType.Date).Value = fechaInicio.Date;
        command.Parameters.Add("@FechaFin", SqlDbType.Date).Value = fechaFin.Date;

        if (ignorarIdBajaParcial.HasValue)
        {
            command.Parameters.Add("@IgnorarIdBajaParcial", SqlDbType.Int).Value =
                ignorarIdBajaParcial.Value;
        }

        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
    }

    private static async Task SincronizarEmpleadoEnTransaccionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int idEmpleado,
        DateTime fechaReferencia,
        CancellationToken ct)
    {
        const string sql = @"
            DECLARE @DisponibilidadAnterior NVARCHAR(50);

            SELECT @DisponibilidadAnterior = e.Disponibilidad
            FROM dbo.Empleados e WITH (UPDLOCK, HOLDLOCK)
            WHERE e.IdEmpleado = @IdEmpleado;

            IF EXISTS
            (
                SELECT 1
                FROM dbo.BajasParciales b WITH (UPDLOCK, HOLDLOCK)
                WHERE b.IdEmpleado = @IdEmpleado
                  AND b.Estado = N'Activa'
                  AND b.FechaInicio <= @FechaReferencia
                  AND b.FechaFin >= @FechaReferencia
            )
            BEGIN
                UPDATE b
                SET b.DisponibilidadAnterior =
                    CASE
                        WHEN b.DisponibilidadAnterior IS NULL
                             OR LTRIM(RTRIM(b.DisponibilidadAnterior)) = N''
                             OR b.DisponibilidadAnterior = N'Baja Parcial'
                            THEN CASE
                                WHEN @DisponibilidadAnterior = N'Baja Parcial'
                                    THEN N'Disponible'
                                ELSE @DisponibilidadAnterior
                            END
                        ELSE b.DisponibilidadAnterior
                    END
                FROM dbo.BajasParciales b
                WHERE b.IdEmpleado = @IdEmpleado
                  AND b.Estado = N'Activa'
                  AND b.FechaInicio <= @FechaReferencia
                  AND b.FechaFin >= @FechaReferencia;

                UPDATE dbo.Empleados
                SET Disponibilidad = N'Baja Parcial',
                    FechaModificacion = SYSDATETIME()
                WHERE IdEmpleado = @IdEmpleado
                  AND Activo = 1;
            END
            ELSE IF @DisponibilidadAnterior = N'Baja Parcial'
                 AND EXISTS (SELECT 1 FROM dbo.Empleados WHERE IdEmpleado = @IdEmpleado AND Activo = 1)
            BEGIN
                SELECT TOP (1) @DisponibilidadAnterior =
                    CASE
                        WHEN NULLIF(LTRIM(RTRIM(b.DisponibilidadAnterior)), N'') IS NULL
                             OR b.DisponibilidadAnterior = N'Baja Parcial'
                            THEN N'Disponible'
                        ELSE b.DisponibilidadAnterior
                    END
                FROM dbo.BajasParciales b
                WHERE b.IdEmpleado = @IdEmpleado
                  AND b.Estado IN (N'Finalizada', N'Cancelada')
                ORDER BY b.FechaFin DESC, b.IdBajaParcial DESC;

                UPDATE dbo.Empleados
                SET Disponibilidad = COALESCE(@DisponibilidadAnterior, N'Disponible'),
                    FechaModificacion = SYSDATETIME()
                WHERE IdEmpleado = @IdEmpleado
                  AND Activo = 1
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM dbo.BajasParciales b
                      WHERE b.IdEmpleado = @IdEmpleado
                        AND b.Estado = N'Activa'
                        AND b.FechaInicio <= @FechaReferencia
                        AND b.FechaFin >= @FechaReferencia
                  );
            END;";

        using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@IdEmpleado", SqlDbType.Int).Value = idEmpleado;
        command.Parameters.Add("@FechaReferencia", SqlDbType.Date).Value = fechaReferencia.Date;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static BajaParcialResponseDto Mapear(SqlDataReader reader)
    {
        return new BajaParcialResponseDto(
            (int)reader["IdBajaParcial"],
            (int)reader["IdEmpleado"],
            (string)reader["CodigoEmpleado"],
            (string)reader["NombreEmpleado"],
            (DateTime)reader["FechaInicio"],
            (DateTime)reader["FechaFin"],
            (string)reader["Motivo"],
            reader["Observaciones"] == DBNull.Value
                ? null
                : (string)reader["Observaciones"],
            (string)reader["Estado"],
            (DateTime)reader["FechaRegistro"],
            reader["FechaModificacion"] == DBNull.Value
                ? null
                : (DateTime)reader["FechaModificacion"]);
    }
    }
