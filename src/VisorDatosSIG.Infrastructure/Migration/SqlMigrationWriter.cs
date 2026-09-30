using Microsoft.Data.SqlClient;
using NetTopologySuite.Geometries;
using System.Data;
using System.Diagnostics;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Infrastructure.Persistence;

namespace VisorDatosSIG.Infrastructure.Migration;

public sealed class SqlMigrationWriter
{
    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlMigrationWriter(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    internal async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = _connectionFactory.Create();
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    internal static async Task<int> GetDestinationRecordCountAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ShapefileLayer layer,
        CancellationToken cancellationToken)
    {
        var table = LayerRecordMapper.GetDestinationTable(layer);
        await using var command = new SqlCommand($"SELECT COUNT(*) FROM {table};", connection, transaction);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is int count ? count : Convert.ToInt32(result);
    }

    internal static async Task<HashSet<string>> GetExistingNaturalKeysAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ShapefileLayer layer,
        CancellationToken cancellationToken)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var query = layer switch
        {
            ShapefileLayer.Manzanas => """
                SELECT IdOrigen, UV_MZA, UV, MZA
                FROM dbo.Manzanas;
                """,
            ShapefileLayer.Lotes => """
                SELECT IdOrigen, NroLote
                FROM dbo.Lotes;
                """,
            ShapefileLayer.CodigosFijos => """
                SELECT CodF_SIG, CodFijo
                FROM dbo.CodigosFijos;
                """,
            ShapefileLayer.Vias => """
                SELECT OSMID, OBJECTID
                FROM dbo.Vias;
                """,
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(query))
        {
            return keys;
        }

        await using var command = new SqlCommand(query, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            switch (layer)
            {
                case ShapefileLayer.Manzanas:
                    if (!reader.IsDBNull(0) && reader.GetInt32(0) > 0)
                    {
                        keys.Add($"ID:{reader.GetInt32(0)}");
                    }
                    if (!reader.IsDBNull(1) && !string.IsNullOrWhiteSpace(reader.GetString(1)))
                    {
                        keys.Add($"UVMZA:{reader.GetString(1).Trim().ToUpperInvariant()}");
                    }
                    break;

                case ShapefileLayer.Lotes:
                    if (!reader.IsDBNull(0) && reader.GetInt32(0) > 0)
                    {
                        keys.Add($"ID:{reader.GetInt32(0)}");
                    }
                    if (!reader.IsDBNull(1) && !string.IsNullOrWhiteSpace(reader.GetString(1)))
                    {
                        keys.Add($"NRO:{reader.GetString(1).Trim().ToUpperInvariant()}");
                    }
                    break;

                case ShapefileLayer.CodigosFijos:
                    if (!reader.IsDBNull(0) && !string.IsNullOrWhiteSpace(reader.GetString(0)))
                    {
                        keys.Add($"SIG:{reader.GetString(0).Trim().ToUpperInvariant()}");
                    }
                    if (!reader.IsDBNull(1) && reader.GetInt32(1) > 0)
                    {
                        keys.Add($"FIJO:{reader.GetInt32(1)}");
                    }
                    break;

                case ShapefileLayer.Vias:
                    if (!reader.IsDBNull(0) && !string.IsNullOrWhiteSpace(reader.GetString(0)))
                    {
                        keys.Add($"OSM:{reader.GetString(0).Trim().ToUpperInvariant()}");
                    }
                    if (!reader.IsDBNull(1) && reader.GetInt32(1) > 0)
                    {
                        keys.Add($"OID:{reader.GetInt32(1)}");
                    }
                    break;
            }
        }

        return keys;
    }

    internal static async Task DeleteDestinationAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ShapefileLayer layer,
        CancellationToken cancellationToken)
    {
        var table = LayerRecordMapper.GetDestinationTable(layer);
        await using var command = new SqlCommand($"DELETE FROM {table};", connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static async Task<MigrationVerificationResult> VerifyAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ShapefileLayer layer,
        CancellationToken cancellationToken)
    {
        var table = LayerRecordMapper.GetDestinationTable(layer);
        var sql = $"""
            SELECT
                COUNT(*) AS DestinationRecords,
                COALESCE(SUM(CASE WHEN Geom IS NULL THEN 1 ELSE 0 END), 0) AS NullGeometryRecords,
                COALESCE(SUM(CASE WHEN Geom IS NOT NULL AND Geom.STSrid <> 4326 THEN 1 ELSE 0 END), 0) AS WrongSridRecords,
                COALESCE(SUM(CASE WHEN Geom IS NOT NULL AND Geom.STIsValid() = 0 THEN 1 ELSE 0 END), 0) AS InvalidGeometryRecords
            FROM {table};
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidDataException("No se pudo obtener la verificación post-carga.");
        }

        return new MigrationVerificationResult
        {
            DestinationRecords = reader.GetInt32(0),
            NullGeometryRecords = reader.GetInt32(1),
            WrongSridRecords = reader.GetInt32(2),
            InvalidGeometryRecords = reader.GetInt32(3)
        };
    }

    internal static async Task InsertAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ShapefileLayer layer,
        MigrationRow row,
        CancellationToken cancellationToken)
    {
        await using var command = CreateInsertCommand(connection, transaction, layer, row);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Reconstruye los índices espaciales de las cuatro capas de la base de datos (RF-MIG-14).
    /// </summary>
    public async Task<(bool Succeeded, string Message)> RebuildSpatialIndexesAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            const string sql = """
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'SIX_Manzanas_Geom')
                    ALTER INDEX SIX_Manzanas_Geom ON dbo.Manzanas REBUILD;

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'SIX_Lotes_Geom')
                    ALTER INDEX SIX_Lotes_Geom ON dbo.Lotes REBUILD;

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'SIX_CodigosFijos_Geom')
                    ALTER INDEX SIX_CodigosFijos_Geom ON dbo.CodigosFijos REBUILD;

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'SIX_Vias_Geom')
                    ALTER INDEX SIX_Vias_Geom ON dbo.Vias REBUILD;
                """;

            await using var command = new SqlCommand(sql, connection);
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync(cancellationToken);
            stopwatch.Stop();

            return (true, $"Índices espaciales reconstruidos correctamente en {stopwatch.Elapsed.TotalSeconds:F2} segundos.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return (false, $"Error al reconstruir índices espaciales: {ex.Message}");
        }
    }

    private static SqlCommand CreateInsertCommand(
        SqlConnection connection,
        SqlTransaction transaction,
        ShapefileLayer layer,
        MigrationRow row)
    {
        var command = new SqlCommand(CreateInsertSql(layer), connection, transaction);

        foreach (var value in row.Values)
        {
            command.Parameters.Add(CreateParameter(value.Key, value.Value));
        }

        var geometryParameter = new SqlParameter("@Geom", SqlDbType.VarBinary)
        {
            Value = row.Geometry.ToBinary()
        };
        command.Parameters.Add(geometryParameter);
        return command;
    }

    private static SqlParameter CreateParameter(string name, object? value)
    {
        var parameter = name switch
        {
            "IdOrigen" or "CodF_SQL" or "CodFijo" or "IdManzana" or "IdLote" or "OBJECTID"
                => new SqlParameter($"@{name}", SqlDbType.Int),
            "Longitud" or "Latitud"
                => new SqlParameter($"@{name}", SqlDbType.Float),
            "UV_MZA" => new SqlParameter($"@{name}", SqlDbType.NVarChar, 20),
            "UV" => new SqlParameter($"@{name}", SqlDbType.NVarChar, 15),
            "MZA" => new SqlParameter($"@{name}", SqlDbType.NVarChar, 10),
            "NroLote" => new SqlParameter($"@{name}", SqlDbType.NVarChar, 15),
            "CodF_SIG" => new SqlParameter($"@{name}", SqlDbType.NVarChar, 25),
            "Nombre" => new SqlParameter($"@{name}", SqlDbType.NVarChar, 120),
            "TipoVia" => new SqlParameter($"@{name}", SqlDbType.NVarChar, 30),
            "OSMID" => new SqlParameter($"@{name}", SqlDbType.NVarChar, 20),
            _ => throw new InvalidOperationException($"Campo de migración no configurado: {name}.")
        };

        parameter.Value = value ?? DBNull.Value;
        return parameter;
    }

    private static string CreateInsertSql(ShapefileLayer layer) => layer switch
    {
        ShapefileLayer.Manzanas => """
            INSERT INTO dbo.Manzanas (IdOrigen, UV_MZA, UV, MZA, Geom)
            VALUES (@IdOrigen, @UV_MZA, @UV, @MZA, geometry::STGeomFromWKB(@Geom, 4326));
            """,
        ShapefileLayer.Lotes => """
            INSERT INTO dbo.Lotes (IdOrigen, NroLote, IdManzana, Geom)
            VALUES (@IdOrigen, @NroLote, @IdManzana, geometry::STGeomFromWKB(@Geom, 4326));
            """,
        ShapefileLayer.CodigosFijos => """
            INSERT INTO dbo.CodigosFijos
                (CodF_SQL, CodF_SIG, CodFijo, Nombre, IdLote, Longitud, Latitud, Geom)
            VALUES
                (@CodF_SQL, @CodF_SIG, @CodFijo, @Nombre, @IdLote, @Longitud, @Latitud,
                 geometry::STGeomFromWKB(@Geom, 4326));
            """,
        ShapefileLayer.Vias => """
            INSERT INTO dbo.Vias (OBJECTID, Nombre, TipoVia, OSMID, Geom)
            VALUES (@OBJECTID, @Nombre, @TipoVia, @OSMID, geometry::STGeomFromWKB(@Geom, 4326));
            """,
        _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, "La capa no está reconocida.")
    };
}
