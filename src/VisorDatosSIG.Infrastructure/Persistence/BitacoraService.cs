using Microsoft.Data.SqlClient;
using System.Data;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Infrastructure.Persistence;

/// <summary>
/// Implementación del servicio de bitácora contra la tabla dbo.Bitacora de SQL Server (RF-MIG-10, CU30).
/// </summary>
public sealed class BitacoraService : IBitacoraService
{
    private readonly SqlServerConnectionFactory _connectionFactory;

    public BitacoraService(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<bool> RegistrarAsync(BitacoraEntryDto entrada, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        try
        {
            await using var connection = _connectionFactory.Create();
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                IF OBJECT_ID('dbo.Bitacora', 'U') IS NOT NULL
                BEGIN
                    INSERT INTO dbo.Bitacora
                        (IdUsuario, FechaHora, Modulo, Accion, Entidad, IdEntidad, Resultado, Detalle, IP)
                    VALUES
                        (@IdUsuario, @FechaHora, @Modulo, @Accion, @Entidad, @IdEntidad, @Resultado, @Detalle, @IP);
                END
                """;

            var fechaHoraBolivia = entrada.FechaHora == default
                ? DateTime.UtcNow.AddHours(-4)
                : (entrada.FechaHora.Kind == DateTimeKind.Utc ? entrada.FechaHora.AddHours(-4) : entrada.FechaHora);

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add(new SqlParameter("@IdUsuario", SqlDbType.Int) { Value = (object?)entrada.IdUsuario ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@FechaHora", SqlDbType.DateTime2) { Value = fechaHoraBolivia });
            command.Parameters.Add(new SqlParameter("@Modulo", SqlDbType.NVarChar, 100) { Value = entrada.Modulo });
            command.Parameters.Add(new SqlParameter("@Accion", SqlDbType.NVarChar, 200) { Value = entrada.Accion });
            command.Parameters.Add(new SqlParameter("@Entidad", SqlDbType.NVarChar, 200) { Value = entrada.Entidad });
            command.Parameters.Add(new SqlParameter("@IdEntidad", SqlDbType.BigInt) { Value = (object?)entrada.IdEntidad ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@Resultado", SqlDbType.NVarChar, 60) { Value = entrada.Resultado });
            command.Parameters.Add(new SqlParameter("@Detalle", SqlDbType.NVarChar, -1) { Value = entrada.Detalle });
            command.Parameters.Add(new SqlParameter("@IP", SqlDbType.VarChar, 45) { Value = (object?)entrada.IP ?? Environment.MachineName });

            await command.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }
        catch
        {
            // La auditoría no debe detener la aplicación si la tabla o conexión no responden
            return false;
        }
    }

    public Task<IReadOnlyList<BitacoraItemDto>> ObtenerHistorialAsync(int limite = 100, CancellationToken cancellationToken = default) =>
        ObtenerHistorialAsync(modulo: null, moduloExcluido: null, limite, cancellationToken);

    public async Task<IReadOnlyList<BitacoraItemDto>> ObtenerHistorialAsync(string? modulo, string? moduloExcluido, int limite = 100, CancellationToken cancellationToken = default)
    {
        var lista = new List<BitacoraItemDto>();
        try
        {
            await using var connection = _connectionFactory.Create();
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                IF OBJECT_ID('dbo.Bitacora', 'U') IS NOT NULL
                BEGIN
                    SELECT TOP (@Limite)
                        IdBitacora, IdUsuario, FechaHora, Modulo, Accion, Entidad, IdEntidad, Resultado, Detalle, IP
                    FROM dbo.Bitacora
                    WHERE (@Modulo IS NULL OR Modulo = @Modulo OR (@Modulo = 'Migrador de Datos Geográficos' AND Modulo = 'MIGRADOR'))
                      AND (@ModuloExcluido IS NULL OR (Modulo <> @ModuloExcluido AND Modulo <> 'MIGRADOR'))
                    ORDER BY FechaHora DESC;
                END
                """;

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add(new SqlParameter("@Limite", SqlDbType.Int) { Value = limite });
            command.Parameters.Add(new SqlParameter("@Modulo", SqlDbType.NVarChar, 100) { Value = (object?)modulo ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@ModuloExcluido", SqlDbType.NVarChar, 100) { Value = (object?)moduloExcluido ?? DBNull.Value });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                lista.Add(new BitacoraItemDto
                {
                    IdBitacora = reader.GetInt64(0),
                    IdUsuario = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    FechaHora = reader.GetDateTime(2),
                    Modulo = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    Accion = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Entidad = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    IdEntidad = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                    Resultado = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    Detalle = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    IP = reader.IsDBNull(9) ? null : reader.GetString(9)
                });
            }
        }
        catch
        {
            // Retorna lo obtenido o lista vacía
        }

        return lista;
    }
}
