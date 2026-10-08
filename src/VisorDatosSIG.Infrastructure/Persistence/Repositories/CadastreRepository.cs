using Microsoft.Data.SqlClient;
using NetTopologySuite.IO;
using System.Data;
using System.Globalization;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.CodigosFijos;
using VisorDatosSIG.Application.DTOs.Manzanas;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Spatial;

namespace VisorDatosSIG.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repositorio de consultas espaciales y alfanuméricas sobre SQL Server 2022 (RT-04, RT-07, RT-08).
/// </summary>
public sealed partial class CadastreRepository : ICadastreRepository
{
    private readonly SqlServerConnectionFactory _connectionFactory;
    private static readonly WKBReader WkbReader = new();

    public CadastreRepository(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<PagedResult<CodigoFijoResumenDto>> SearchCodigosFijosAsync(
        CodigoFijoConsultaDto consulta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var pagina = Math.Max(1, consulta.Pagina);
        var limite = Math.Clamp(consulta.Limite, 1, 100);
        var offset = ((long)pagina - 1) * limite;

        const string where = """
            WHERE (@CodFSig IS NULL OR CodF_SIG LIKE @CodFSig)
              AND (@CodFijo IS NULL OR CodFijo = @CodFijo)
              AND (@Nombre IS NULL OR Nombre LIKE @Nombre)
              AND (@Estado IS NULL OR Estado = @Estado)
            """;

        var sqlConteo = $"SELECT COUNT(*) FROM dbo.CodigosFijos {where};";
        var sqlDatos = $"""
            SELECT IdCodigo, CodF_SIG, CodFijo, Nombre, Estado
            FROM dbo.CodigosFijos
            {where}
            ORDER BY IdCodigo
            OFFSET @Offset ROWS FETCH NEXT @Limite ROWS ONLY;
            """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        int total;
        await using (var command = new SqlCommand(sqlConteo, connection))
        {
            AddCodigoFijoFilterParameters(command, consulta);
            total = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        }

        var datos = new List<CodigoFijoResumenDto>();
        await using (var command = new SqlCommand(sqlDatos, connection))
        {
            AddCodigoFijoFilterParameters(command, consulta);
            command.Parameters.Add(new SqlParameter("@Offset", SqlDbType.BigInt) { Value = offset });
            command.Parameters.Add(new SqlParameter("@Limite", SqlDbType.Int) { Value = limite });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                datos.Add(new CodigoFijoResumenDto
                {
                    IdCodigo = reader.GetInt32(0),
                    CodFSig = reader.IsDBNull(1) ? null : reader.GetString(1),
                    CodFijo = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    Nombre = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Estado = reader.GetByte(4)
                });
            }
        }

        return new PagedResult<CodigoFijoResumenDto>
        {
            Pagina = pagina,
            Limite = limite,
            TotalRegistros = total,
            Datos = datos
        };
    }

    public async Task<CodigoFijoDetalleDto?> GetCodigoFijoByIdAsync(
        int idCodigo,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT IdCodigo, CodF_SQL, CodF_SIG, CodFijo, Nombre, Estado,
                   FechaCambioEstado, IdLote, Longitud, Latitud,
                   Geom.STAsBinary() AS GeomWkb
            FROM dbo.CodigosFijos
            WHERE IdCodigo = @IdCodigo;
            """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@IdCodigo", SqlDbType.Int) { Value = idCodigo });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        object? geometria = null;
        if (!reader.IsDBNull(10))
        {
            var wkb = (byte[])reader.GetValue(10);
            geometria = SpatialGeoJsonHelper.ToGeoJsonObject(WkbReader.Read(wkb));
        }

        return new CodigoFijoDetalleDto
        {
            IdCodigo = reader.GetInt32(0),
            CodFSql = reader.IsDBNull(1) ? null : reader.GetInt32(1),
            CodFSig = reader.IsDBNull(2) ? null : reader.GetString(2),
            CodFijo = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            Nombre = reader.IsDBNull(4) ? null : reader.GetString(4),
            Estado = reader.GetByte(5),
            FechaCambioEstado = reader.GetDateTime(6),
            IdLote = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            Longitud = reader.IsDBNull(8) ? null : reader.GetDouble(8),
            Latitud = reader.IsDBNull(9) ? null : reader.GetDouble(9),
            Geometria = geometria
        };
    }

    private static void AddCodigoFijoFilterParameters(SqlCommand command, CodigoFijoConsultaDto consulta)
    {
        command.Parameters.Add(new SqlParameter("@CodFSig", SqlDbType.NVarChar, 4000)
        {
            Value = string.IsNullOrWhiteSpace(consulta.CodFSig)
                ? DBNull.Value
                : $"%{consulta.CodFSig.Trim()}%"
        });
        command.Parameters.Add(new SqlParameter("@CodFijo", SqlDbType.Int)
        {
            Value = (object?)consulta.CodFijo ?? DBNull.Value
        });
        command.Parameters.Add(new SqlParameter("@Nombre", SqlDbType.NVarChar, 4000)
        {
            Value = string.IsNullOrWhiteSpace(consulta.Nombre)
                ? DBNull.Value
                : $"%{consulta.Nombre.Trim()}%"
        });
        command.Parameters.Add(new SqlParameter("@Estado", SqlDbType.TinyInt)
        {
            Value = (object?)consulta.Estado ?? DBNull.Value
        });
    }

    public async Task<PagedResult<ManzanaResumenDto>> SearchManzanasAsync(
        ManzanaConsultaDto consulta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var pagina = Math.Max(1, consulta.Pagina);
        var limite = Math.Clamp(consulta.Limite, 1, 100);
        var offset = ((long)pagina - 1) * limite;

        const string where = """
            WHERE (@UvMza IS NULL OR UV_MZA LIKE @UvMza)
              AND (@Uv IS NULL OR UV LIKE @Uv)
              AND (@Mza IS NULL OR MZA LIKE @Mza)
            """;

        var sqlConteo = $"SELECT COUNT(*) FROM dbo.Manzanas {where};";
        var sqlDatos = $"""
            SELECT IdManzana, UV_MZA, UV, MZA
            FROM dbo.Manzanas
            {where}
            ORDER BY UV, MZA, IdManzana
            OFFSET @Offset ROWS FETCH NEXT @Limite ROWS ONLY;
            """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        int total;
        await using (var command = new SqlCommand(sqlConteo, connection))
        {
            AddManzanaFilterParameters(command, consulta);
            total = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        }

        var datos = new List<ManzanaResumenDto>();
        await using (var command = new SqlCommand(sqlDatos, connection))
        {
            AddManzanaFilterParameters(command, consulta);
            command.Parameters.Add(new SqlParameter("@Offset", SqlDbType.BigInt) { Value = offset });
            command.Parameters.Add(new SqlParameter("@Limite", SqlDbType.Int) { Value = limite });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                datos.Add(new ManzanaResumenDto
                {
                    IdManzana = reader.GetInt32(0),
                    UvMza = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Uv = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Mza = reader.IsDBNull(3) ? null : reader.GetString(3)
                });
            }
        }

        return new PagedResult<ManzanaResumenDto>
        {
            Pagina = pagina,
            Limite = limite,
            TotalRegistros = total,
            Datos = datos
        };
    }

    public async Task<ManzanaDetalleDto?> GetManzanaByIdAsync(
        int idManzana,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT IdManzana, IdOrigen, UV_MZA, UV, MZA,
                   Geom.STAsBinary() AS GeomWkb
            FROM dbo.Manzanas
            WHERE IdManzana = @IdManzana;
            """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@IdManzana", SqlDbType.Int) { Value = idManzana });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        object? geometria = null;
        if (!reader.IsDBNull(5))
        {
            var wkb = (byte[])reader.GetValue(5);
            geometria = SpatialGeoJsonHelper.ToGeoJsonObject(WkbReader.Read(wkb));
        }

        return new ManzanaDetalleDto
        {
            IdManzana = reader.GetInt32(0),
            IdOrigen = reader.IsDBNull(1) ? null : reader.GetInt32(1),
            UvMza = reader.IsDBNull(2) ? null : reader.GetString(2),
            Uv = reader.IsDBNull(3) ? null : reader.GetString(3),
            Mza = reader.IsDBNull(4) ? null : reader.GetString(4),
            Geometria = geometria
        };
    }

    private static void AddManzanaFilterParameters(SqlCommand command, ManzanaConsultaDto consulta)
    {
        command.Parameters.Add(new SqlParameter("@UvMza", SqlDbType.NVarChar, 4000)
        {
            Value = LikeOrDbNull(consulta.UvMza)
        });
        command.Parameters.Add(new SqlParameter("@Uv", SqlDbType.NVarChar, 4000)
        {
            Value = LikeOrDbNull(consulta.Uv)
        });
        command.Parameters.Add(new SqlParameter("@Mza", SqlDbType.NVarChar, 4000)
        {
            Value = LikeOrDbNull(consulta.Mza)
        });
    }

    private static object LikeOrDbNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : $"%{value.Trim()}%";

    public async Task<GeoJsonFeatureCollectionDto> GetLayerGeoJsonAsync(
        string layerName,
        double? minX = null,
        double? minY = null,
        double? maxX = null,
        double? maxY = null,
        int limit = 2000,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit <= 0 ? 2000 : limit, 1, 10000);
        var features = new List<GeoJsonFeatureDto>();
        var (tableName, idCol, selectCols) = ResolveLayerInfo(layerName);

        if (string.IsNullOrEmpty(tableName))
        {
            return new GeoJsonFeatureCollectionDto { Features = features };
        }

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var sql = $"SELECT TOP (@Limit) {idCol}, {selectCols}, Geom.STAsBinary() AS GeomWkb FROM {tableName} WHERE Geom IS NOT NULL";

        if (minX.HasValue && minY.HasValue && maxX.HasValue && maxY.HasValue)
        {
            var polyWkt = string.Format(
                CultureInfo.InvariantCulture,
                "POLYGON(({0} {1}, {2} {1}, {2} {3}, {0} {3}, {0} {1}))",
                minX.Value, minY.Value, maxX.Value, maxY.Value);

            sql += $" AND Geom.STIntersects(geometry::STPolyFromText('{polyWkt}', 4326)) = 1";
        }

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@Limit", SqlDbType.Int) { Value = limit });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetValue(0);
            var properties = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            for (var i = 1; i < reader.FieldCount - 1; i++)
            {
                properties[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            object? geometryObject = null;
            if (!reader.IsDBNull(reader.FieldCount - 1))
            {
                var bytes = (byte[])reader.GetValue(reader.FieldCount - 1);
                var geom = WkbReader.Read(bytes);
                geometryObject = SpatialGeoJsonHelper.ToGeoJsonObject(geom);
            }

            features.Add(new GeoJsonFeatureDto
            {
                Id = id,
                Geometry = geometryObject,
                Properties = properties
            });
        }

        return new GeoJsonFeatureCollectionDto { Features = features };
    }

    public async Task<IReadOnlyList<GeoJsonFeatureDto>> IdentifyAsync(
        double longitude,
        double latitude,
        double toleranceMeters = 10,
        CancellationToken cancellationToken = default)
    {
        var features = new List<GeoJsonFeatureDto>();
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        // Convertir tolerancia en metros aprox. a grados (1 grado ~ 111,000 metros)
        var toleranceDegrees = Math.Max(0.00005, toleranceMeters / 111000.0);
        var pointWkt = string.Format(CultureInfo.InvariantCulture, "POINT({0} {1})", longitude, latitude);

        string[] layers = ["dbo.CodigosFijos", "dbo.Lotes", "dbo.Manzanas", "dbo.Vias"];

        foreach (var layer in layers)
        {
            var (tableName, idCol, selectCols) = ResolveLayerInfo(layer);
            var sql = $"""
                SELECT TOP (5) {idCol}, {selectCols}, Geom.STAsBinary() AS GeomWkb
                FROM {tableName}
                WHERE Geom IS NOT NULL
                  AND Geom.STIntersects(geometry::STPointFromText('{pointWkt}', 4326).STBuffer({toleranceDegrees.ToString(CultureInfo.InvariantCulture)})) = 1;
                """;

            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetValue(0);
                var properties = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["_Capa"] = tableName.Replace("dbo.", "")
                };

                for (var i = 1; i < reader.FieldCount - 1; i++)
                {
                    properties[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                object? geometryObject = null;
                if (!reader.IsDBNull(reader.FieldCount - 1))
                {
                    var bytes = (byte[])reader.GetValue(reader.FieldCount - 1);
                    var geom = WkbReader.Read(bytes);
                    geometryObject = SpatialGeoJsonHelper.ToGeoJsonObject(geom);
                }

                features.Add(new GeoJsonFeatureDto
                {
                    Id = id,
                    Geometry = geometryObject,
                    Properties = properties
                });
            }
        }

        return features;
    }

    public async Task<PagedResult<IDictionary<string, object?>>> SearchAsync(
        string query,
        string? layer = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var items = new List<IDictionary<string, object?>>();
        if (string.IsNullOrWhiteSpace(query))
        {
            return new PagedResult<IDictionary<string, object?>> { Pagina = page, Limite = pageSize, Datos = items };
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        // Búsqueda unificada multilínea en vistas o tablas con LIKE parametrizado
        const string sql = """
            WITH BusquedaUnificada AS (
                SELECT 'Manzanas' AS Capa, CAST(IdManzana AS NVARCHAR(50)) AS Clave, UV_MZA AS Titulo,
                       CONCAT('UV: ', UV, ', MZA: ', MZA) AS Subtitulo
                FROM dbo.Manzanas
                WHERE UV_MZA LIKE @Q OR UV LIKE @Q OR MZA LIKE @Q

                UNION ALL

                SELECT 'Lotes' AS Capa, CAST(IdLote AS NVARCHAR(50)) AS Clave, CONCAT('Lote: ', NroLote) AS Titulo,
                       CONCAT('Origen: ', IdOrigen) AS Subtitulo
                FROM dbo.Lotes
                WHERE NroLote LIKE @Q

                UNION ALL

                SELECT 'CodigosFijos' AS Capa, CAST(IdCodigo AS NVARCHAR(50)) AS Clave, CodF_SIG AS Titulo,
                       Nombre AS Subtitulo
                FROM dbo.CodigosFijos
                WHERE CodF_SIG LIKE @Q OR CAST(CodFijo AS NVARCHAR(30)) LIKE @Q OR Nombre LIKE @Q

                UNION ALL

                SELECT 'Vias' AS Capa, CAST(IdVia AS NVARCHAR(50)) AS Clave, Nombre AS Titulo,
                       CONCAT('Tipo: ', TipoVia, ', OSM: ', OSMID) AS Subtitulo
                FROM dbo.Vias
                WHERE Nombre LIKE @Q OR TipoVia LIKE @Q OR OSMID LIKE @Q
            )
            SELECT Capa, Clave, Titulo, Subtitulo, COUNT(*) OVER() AS TotalCount
            FROM BusquedaUnificada
            WHERE (@Capa IS NULL OR Capa = @Capa)
            ORDER BY Capa, Titulo
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@Q", SqlDbType.NVarChar, 100) { Value = $"%{query.Trim()}%" });
        command.Parameters.Add(new SqlParameter("@Capa", SqlDbType.NVarChar, 50) { Value = (object?)layer ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@Offset", SqlDbType.Int) { Value = offset });
        command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize });

        var total = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            total = reader.GetInt32(4);
            items.Add(new Dictionary<string, object?>
            {
                ["Capa"] = reader.GetString(0),
                ["Clave"] = reader.GetString(1),
                ["Titulo"] = reader.IsDBNull(2) ? "-" : reader.GetString(2),
                ["Subtitulo"] = reader.IsDBNull(3) ? "-" : reader.GetString(3)
            });
        }

        return new PagedResult<IDictionary<string, object?>>
        {
            Pagina = page,
            Limite = pageSize,
            TotalRegistros = total,
            Datos = items
        };
    }

    public async Task<IReadOnlyList<IDictionary<string, object?>>> GetLayersCatalogAsync(CancellationToken cancellationToken = default)
    {
        var lista = new List<IDictionary<string, object?>>();
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT 'Manzanas' AS Capa, 'Delimitación de manzanas' AS Descripcion, 'Polygon' AS Geometria, COUNT(*) AS TotalRegistros FROM dbo.Manzanas
            UNION ALL
            SELECT 'Lotes' AS Capa, 'Parcelas o lotes' AS Descripcion, 'Polygon' AS Geometria, COUNT(*) AS TotalRegistros FROM dbo.Lotes
            UNION ALL
            SELECT 'CodigosFijos' AS Capa, 'Puntos de códigos fijos' AS Descripcion, 'Point' AS Geometria, COUNT(*) AS TotalRegistros FROM dbo.CodigosFijos
            UNION ALL
            SELECT 'Vias' AS Capa, 'Ejes viales' AS Descripcion, 'LineString' AS Geometria, COUNT(*) AS TotalRegistros FROM dbo.Vias;
            """;

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            lista.Add(new Dictionary<string, object?>
            {
                ["Capa"] = reader.GetString(0),
                ["Descripcion"] = reader.GetString(1),
                ["TipoGeometria"] = reader.GetString(2),
                ["TotalRegistros"] = reader.GetInt32(3)
            });
        }

        return lista;
    }

    public async Task<double[]?> GetLayerExtentAsync(string? layerName = null, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        string sql;
        if (string.IsNullOrWhiteSpace(layerName) ||
            layerName.Trim().Equals("todas", StringComparison.OrdinalIgnoreCase) ||
            layerName.Trim().Equals("global", StringComparison.OrdinalIgnoreCase) ||
            layerName.Trim() == "*")
        {
            // Extensión global calculada abarcando todas las capas espaciales existentes
            sql = """
                WITH Extents AS (
                    SELECT MIN(Geom.STEnvelope().STPointN(1).STX) AS MinX, MIN(Geom.STEnvelope().STPointN(1).STY) AS MinY, MAX(Geom.STEnvelope().STPointN(3).STX) AS MaxX, MAX(Geom.STEnvelope().STPointN(3).STY) AS MaxY FROM dbo.Manzanas WHERE Geom IS NOT NULL
                    UNION ALL
                    SELECT MIN(Geom.STEnvelope().STPointN(1).STX), MIN(Geom.STEnvelope().STPointN(1).STY), MAX(Geom.STEnvelope().STPointN(3).STX), MAX(Geom.STEnvelope().STPointN(3).STY) FROM dbo.Lotes WHERE Geom IS NOT NULL
                    UNION ALL
                    SELECT MIN(Geom.STEnvelope().STPointN(1).STX), MIN(Geom.STEnvelope().STPointN(1).STY), MAX(Geom.STEnvelope().STPointN(3).STX), MAX(Geom.STEnvelope().STPointN(3).STY) FROM dbo.CodigosFijos WHERE Geom IS NOT NULL
                    UNION ALL
                    SELECT MIN(Geom.STEnvelope().STPointN(1).STX), MIN(Geom.STEnvelope().STPointN(1).STY), MAX(Geom.STEnvelope().STPointN(3).STX), MAX(Geom.STEnvelope().STPointN(3).STY) FROM dbo.Vias WHERE Geom IS NOT NULL
                )
                SELECT MIN(MinX), MIN(MinY), MAX(MaxX), MAX(MaxY) FROM Extents WHERE MinX IS NOT NULL;
                """;
        }
        else
        {
            var (table, _, _) = ResolveLayerInfo(layerName);
            if (string.IsNullOrEmpty(table))
            {
                return null;
            }

            sql = $"SELECT MIN(Geom.STEnvelope().STPointN(1).STX), MIN(Geom.STEnvelope().STPointN(1).STY), MAX(Geom.STEnvelope().STPointN(3).STX), MAX(Geom.STEnvelope().STPointN(3).STY) FROM {table} WHERE Geom IS NOT NULL";
        }

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (await reader.ReadAsync(cancellationToken) && !reader.IsDBNull(0) && !reader.IsDBNull(1) && !reader.IsDBNull(2) && !reader.IsDBNull(3))
        {
            return
            [
                Convert.ToDouble(reader.GetValue(0)),
                Convert.ToDouble(reader.GetValue(1)),
                Convert.ToDouble(reader.GetValue(2)),
                Convert.ToDouble(reader.GetValue(3))
            ];
        }

        return null;
    }

    private static (string tableName, string idCol, string selectCols) ResolveLayerInfo(string layerName)
    {
        var normalized = layerName.Trim().ToLowerInvariant();
        if (normalized.Contains("manzana") || normalized == "mza")
        {
            return ("dbo.Manzanas", "IdManzana", "IdOrigen, UV_MZA, UV, MZA");
        }

        if (normalized.Contains("lote"))
        {
            return ("dbo.Lotes", "IdLote", "IdOrigen, NroLote, IdManzana");
        }

        if (normalized.Contains("codigofijo") || normalized.Contains("codigo") || normalized.Contains("codfijo"))
        {
            return ("dbo.CodigosFijos", "IdCodigo", "CodF_SQL, CodF_SIG, CodFijo, Nombre, Estado, IdLote, Longitud, Latitud");
        }

        if (normalized.Contains("via"))
        {
            return ("dbo.Vias", "IdVia", "OBJECTID, Nombre, TipoVia, OSMID");
        }

        return (string.Empty, string.Empty, string.Empty);
    }
}
