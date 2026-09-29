using Microsoft.Data.SqlClient;
using NetTopologySuite.Geometries;
using System.Data;
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
