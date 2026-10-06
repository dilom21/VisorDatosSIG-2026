using System.Data;
using Microsoft.Data.SqlClient;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Servicios;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Infrastructure.Data;

public sealed class ServicioRepository : IServicioRepository
{
    private const int LimitePorDefecto = 20;
    private const int LimiteMaximo = 100;
    private readonly SqlConnectionFactory _connectionFactory;

    public ServicioRepository(SqlConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public async Task<PagedResult<ServicioDto>> ObtenerTodosAsync(
        string? busqueda = null,
        byte? estado = null,
        int pagina = 1,
        int limite = LimitePorDefecto,
        CancellationToken ct = default)
    {
        pagina = Math.Max(1, pagina);
        limite = Math.Clamp(limite <= 0 ? LimitePorDefecto : limite, 1, LimiteMaximo);
        var offset = checked((pagina - 1) * limite);

        await using var connection = await _connectionFactory.AbrirAsync(ct);
        const string sql = """
            SELECT IdCodigo, CodF_SQL, CodF_SIG, CodFijo, Nombre, Estado,
                   FechaCambioEstado, IdLote, Longitud, Latitud,
                   COUNT(*) OVER() AS TotalRegistros
            FROM dbo.CodigosFijos
            WHERE (@Busqueda IS NULL
                   OR CodF_SIG LIKE @Busqueda
                   OR CONVERT(nvarchar(30), CodFijo) LIKE @Busqueda
                   OR Nombre LIKE @Busqueda
                   OR CONVERT(nvarchar(30), CodF_SQL) LIKE @Busqueda)
              AND (@Estado IS NULL OR Estado = @Estado)
            ORDER BY IdCodigo ASC
            OFFSET @Offset ROWS FETCH NEXT @Limite ROWS ONLY;
            """;

        await using var command = new SqlCommand(sql, connection);
        AgregarFiltros(command, busqueda, estado);
        command.Parameters.Add("@Offset", SqlDbType.Int).Value = offset;
        command.Parameters.Add("@Limite", SqlDbType.Int).Value = limite;

        var datos = new List<ServicioDto>();
        var total = 0;
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            total = reader.GetInt32(reader.GetOrdinal("TotalRegistros"));
            datos.Add(Mapear(reader));
        }

        return new PagedResult<ServicioDto>
        {
            Pagina = pagina,
            Limite = limite,
            TotalRegistros = total,
            Datos = datos
        };
    }

    public async Task<ServicioDto?> ObtenerPorIdAsync(int idCodigo, CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.AbrirAsync(ct);
        await using var command = new SqlCommand(SelectBase + " WHERE IdCodigo = @IdCodigo;", connection);
        command.Parameters.Add("@IdCodigo", SqlDbType.Int).Value = idCodigo;
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Mapear(reader) : null;
    }

    public async Task<CambioEstadoServicioDto?> CambiarEstadoAsync(
        int idCodigo,
        byte estadoNuevo,
        CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.AbrirAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        try
        {
            const string sqlActual = """
                SELECT IdCodigo, Estado
                FROM dbo.CodigosFijos WITH (UPDLOCK, ROWLOCK)
                WHERE IdCodigo = @IdCodigo;
                """;
            await using var actualCommand = new SqlCommand(sqlActual, connection, transaction);
            actualCommand.Parameters.Add("@IdCodigo", SqlDbType.Int).Value = idCodigo;
            await using var actualReader = await actualCommand.ExecuteReaderAsync(ct);
            if (!await actualReader.ReadAsync(ct))
            {
                await transaction.RollbackAsync(ct);
                return null;
            }

            var estadoAnterior = actualReader.GetByte(actualReader.GetOrdinal("Estado"));
            await actualReader.CloseAsync();

            const string sqlActualizar = """
                UPDATE dbo.CodigosFijos
                SET Estado = @Estado
                WHERE IdCodigo = @IdCodigo;
                """;
            await using var updateCommand = new SqlCommand(sqlActualizar, connection, transaction);
            updateCommand.Parameters.Add("@Estado", SqlDbType.TinyInt).Value = estadoNuevo;
            updateCommand.Parameters.Add("@IdCodigo", SqlDbType.Int).Value = idCodigo;
            await updateCommand.ExecuteNonQueryAsync(ct);

            await using var detailCommand = new SqlCommand(SelectBase + " WHERE IdCodigo = @IdCodigo;", connection, transaction);
            detailCommand.Parameters.Add("@IdCodigo", SqlDbType.Int).Value = idCodigo;
            await using var detailReader = await detailCommand.ExecuteReaderAsync(ct);
            if (!await detailReader.ReadAsync(ct))
            {
                await transaction.RollbackAsync(ct);
                return null;
            }

            var servicio = Mapear(detailReader);
            await detailReader.CloseAsync();
            await transaction.CommitAsync(ct);
            return new CambioEstadoServicioDto(servicio, estadoAnterior, servicio.Estado);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<ServicioResumenDto> ObtenerResumenAsync(CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.AbrirAsync(ct);
        const string sql = """
            SELECT
                COUNT(*) AS Total,
                COALESCE(SUM(CASE WHEN Estado = 1 THEN 1 ELSE 0 END), 0) AS Normal,
                COALESCE(SUM(CASE WHEN Estado = 2 THEN 1 ELSE 0 END), 0) AS ParaCorte,
                COALESCE(SUM(CASE WHEN Estado = 3 THEN 1 ELSE 0 END), 0) AS Cortado,
                COALESCE(SUM(CASE WHEN Estado = 4 THEN 1 ELSE 0 END), 0) AS BajaParcial,
                COALESCE(SUM(CASE WHEN Estado = 5 THEN 1 ELSE 0 END), 0) AS BajaTotal
            FROM dbo.CodigosFijos;
            """;
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return new ServicioResumenDto(
            reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2),
            reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5));
    }

    private const string SelectBase = """
        SELECT IdCodigo, CodF_SQL, CodF_SIG, CodFijo, Nombre, Estado,
               FechaCambioEstado, IdLote, Longitud, Latitud
        FROM dbo.CodigosFijos
        """;

    private static void AgregarFiltros(SqlCommand command, string? busqueda, byte? estado)
    {
        command.Parameters.Add("@Busqueda", SqlDbType.NVarChar, 120).Value =
            string.IsNullOrWhiteSpace(busqueda) ? DBNull.Value : $"%{busqueda.Trim()}%";
        command.Parameters.Add("@Estado", SqlDbType.TinyInt).Value = estado.HasValue ? estado.Value : DBNull.Value;
    }

    private static ServicioDto Mapear(SqlDataReader reader)
    {
        var estado = reader.GetByte(reader.GetOrdinal("Estado"));
        return new ServicioDto(
            reader.GetInt32(reader.GetOrdinal("IdCodigo")),
            LeerNullable<int>(reader, "CodF_SQL"),
            LeerNullable<string>(reader, "CodF_SIG"),
            LeerNullable<int>(reader, "CodFijo"),
            LeerNullable<string>(reader, "Nombre"),
            estado,
            NombreEstado(estado),
            reader.GetDateTime(reader.GetOrdinal("FechaCambioEstado")),
            LeerNullable<int>(reader, "IdLote"),
            LeerNullable<double>(reader, "Longitud"),
            LeerNullable<double>(reader, "Latitud"));
    }

    private static T? LeerNullable<T>(SqlDataReader reader, string columna)
    {
        var ordinal = reader.GetOrdinal(columna);
        return reader.IsDBNull(ordinal) ? default : (T)reader.GetValue(ordinal);
    }

    public static string NombreEstado(byte estado) => estado switch
    {
        1 => "Normal",
        2 => "Para Corte",
        3 => "Cortado",
        4 => "Baja Parcial",
        5 => "Baja Total",
        _ => "Desconocido"
    };
}
