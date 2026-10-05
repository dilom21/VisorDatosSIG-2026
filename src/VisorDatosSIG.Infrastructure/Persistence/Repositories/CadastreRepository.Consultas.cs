using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Lotes;
using VisorDatosSIG.Application.DTOs.Vias;
using VisorDatosSIG.Infrastructure.Spatial;

namespace VisorDatosSIG.Infrastructure.Persistence.Repositories;

public sealed partial class CadastreRepository
{

    public async Task<PagedResult<LoteResumenDto>> SearchLotesAsync(
        LoteConsultaDto consulta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        var pagina = Math.Max(1, consulta.Pagina);
        var limite = Math.Clamp(consulta.Limite, 1, 100);
        const string where = "WHERE (@NroLote IS NULL OR NroLote LIKE @NroLote) AND (@IdManzana IS NULL OR IdManzana = @IdManzana)";
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        int total;
        await using (var command = new SqlCommand($"SELECT COUNT(*) FROM dbo.Lotes {where};", connection))
        {
            AddLoteParameters(command, consulta);
            total = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        }
        var datos = new List<LoteResumenDto>();
        await using (var command = new SqlCommand(
            $"SELECT IdLote, NroLote, IdManzana FROM dbo.Lotes {where} ORDER BY IdLote OFFSET @Offset ROWS FETCH NEXT @Limite ROWS ONLY;", connection))
        {
            AddLoteParameters(command, consulta);
            command.Parameters.Add(new SqlParameter("@Offset", SqlDbType.BigInt) { Value = ((long)pagina - 1) * limite });
            command.Parameters.Add(new SqlParameter("@Limite", SqlDbType.Int) { Value = limite });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                datos.Add(new LoteResumenDto
                {
                    IdLote = reader.GetInt32(0),
                    NroLote = reader.IsDBNull(1) ? null : reader.GetString(1),
                    IdManzana = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                });
            }
        }
        return new PagedResult<LoteResumenDto> { Pagina = pagina, Limite = limite, TotalRegistros = total, Datos = datos };
    }

    public async Task<LoteDetalleDto?> GetLoteByIdAsync(int idLote, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT IdLote, NroLote, IdManzana, IdOrigen, Geom.STAsBinary() FROM dbo.Lotes WHERE IdLote = @Id;";
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = idLote });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var ordinalGeom = 4;
        return new LoteDetalleDto
        {
            IdLote = reader.GetInt32(0),
            NroLote = reader.IsDBNull(1) ? null : reader.GetString(1),
            IdManzana = reader.IsDBNull(2) ? null : reader.GetInt32(2),
            IdOrigen = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            Geometria = reader.IsDBNull(ordinalGeom) ? null
                : SpatialGeoJsonHelper.ToGeoJsonObject(new NetTopologySuite.IO.WKBReader().Read((byte[])reader.GetValue(ordinalGeom)))
        };
    }

    private static void AddLoteParameters(SqlCommand command, LoteConsultaDto consulta)
    {
        command.Parameters.Add(new SqlParameter("@NroLote", SqlDbType.NVarChar, 4000) { Value = LikeOrDbNull(consulta.NroLote) });
        command.Parameters.Add(new SqlParameter("@IdManzana", SqlDbType.Int) { Value = (object?)consulta.IdManzana ?? DBNull.Value });
    }

    public async Task<PagedResult<ViaResumenDto>> SearchViasAsync(
        ViaConsultaDto consulta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        var pagina = Math.Max(1, consulta.Pagina);
        var limite = Math.Clamp(consulta.Limite, 1, 100);
        const string where = "WHERE (@Nombre IS NULL OR Nombre LIKE @Nombre) AND (@TipoVia IS NULL OR TipoVia LIKE @TipoVia) AND (@Osmid IS NULL OR OSMID LIKE @Osmid)";
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        int total;
        await using (var command = new SqlCommand($"SELECT COUNT(*) FROM dbo.Vias {where};", connection))
        {
            AddViaParameters(command, consulta);
            total = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        }
        var datos = new List<ViaResumenDto>();
        await using (var command = new SqlCommand(
            $"SELECT IdVia, Nombre, TipoVia, OSMID FROM dbo.Vias {where} ORDER BY IdVia OFFSET @Offset ROWS FETCH NEXT @Limite ROWS ONLY;", connection))
        {
            AddViaParameters(command, consulta);
            command.Parameters.Add(new SqlParameter("@Offset", SqlDbType.BigInt) { Value = ((long)pagina - 1) * limite });
            command.Parameters.Add(new SqlParameter("@Limite", SqlDbType.Int) { Value = limite });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                datos.Add(new ViaResumenDto
                {
                    IdVia = reader.GetInt32(0),
                    Nombre = reader.IsDBNull(1) ? null : reader.GetString(1),
                    TipoVia = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Osmid = reader.IsDBNull(3) ? null : reader.GetString(3),
                });
            }
        }
        return new PagedResult<ViaResumenDto> { Pagina = pagina, Limite = limite, TotalRegistros = total, Datos = datos };
    }

    public async Task<ViaDetalleDto?> GetViaByIdAsync(int idVia, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT IdVia, Nombre, TipoVia, OSMID, OBJECTID, Geom.STAsBinary() FROM dbo.Vias WHERE IdVia = @Id;";
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = idVia });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var ordinalGeom = 5;
        return new ViaDetalleDto
        {
            IdVia = reader.GetInt32(0),
            Nombre = reader.IsDBNull(1) ? null : reader.GetString(1),
            TipoVia = reader.IsDBNull(2) ? null : reader.GetString(2),
            Osmid = reader.IsDBNull(3) ? null : reader.GetString(3),
            Objectid = reader.IsDBNull(4) ? null : reader.GetInt32(4),
            Geometria = reader.IsDBNull(ordinalGeom) ? null
                : SpatialGeoJsonHelper.ToGeoJsonObject(new NetTopologySuite.IO.WKBReader().Read((byte[])reader.GetValue(ordinalGeom)))
        };
    }

    private static void AddViaParameters(SqlCommand command, ViaConsultaDto consulta)
    {
        command.Parameters.Add(new SqlParameter("@Nombre", SqlDbType.NVarChar, 4000) { Value = LikeOrDbNull(consulta.Nombre) });
        command.Parameters.Add(new SqlParameter("@TipoVia", SqlDbType.NVarChar, 4000) { Value = LikeOrDbNull(consulta.TipoVia) });
        command.Parameters.Add(new SqlParameter("@Osmid", SqlDbType.NVarChar, 4000) { Value = LikeOrDbNull(consulta.Osmid) });
    }
}
