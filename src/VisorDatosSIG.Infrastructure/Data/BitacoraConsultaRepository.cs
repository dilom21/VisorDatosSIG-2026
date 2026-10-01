using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Bitacora;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Infrastructure.Data;

/// <summary>
/// Consulta paginada de la bitácora del sistema web (CU05 - Consultar Bitácora).
/// </summary>
/// <remarks>
/// Decisiones de la implementación:
/// <list type="bullet">
/// <item>El módulo de migración se excluye <b>en SQL</b> (<c>b.Modulo &lt;&gt; @ModuloExcluido</c>), en
/// todas las consultas: listado, catálogos y detalle. La exclusión no depende de la petición.</item>
/// <item>La paginación y el conteo se resuelven en un solo viaje, con <c>COUNT_BIG(*)</c> y
/// <c>OFFSET/FETCH</c>: la base devuelve únicamente la página solicitada.</item>
/// <item>Los filtros viajan siempre como parámetros (incluido el patrón de <c>LIKE</c>, cuyos
/// comodines se escapan con corchetes); nunca se concatena texto del cliente.</item>
/// <item>El proyecto del usuario se resuelve con <c>LEFT JOIN</c>, así que un evento sin usuario
/// (por ejemplo un intento con login inexistente) sigue apareciendo con los campos de usuario
/// nulos.</item>
/// </list>
/// </remarks>
public sealed class BitacoraConsultaRepository : IBitacoraConsultaRepository
{
    /// <summary>Módulo que la consulta web nunca expone.</summary>
    private const string ModuloExcluido = ModulosSistema.MigradorDeDatosGeograficos;

    private const int LongitudModulo = 100;
    private const int LongitudAccion = 200;
    private const int LongitudEntidad = 200;
    private const int LongitudResultado = 60;
    private const int LongitudBusqueda = 200;

    /// <summary>Proyección común de un evento con los datos del usuario que lo generó.</summary>
    private const string ColumnasEvento = """
        b.IdBitacora,
        b.IdUsuario,
        u.Login AS LoginUsuario,
        u.Nombre AS NombreUsuario,
        b.FechaHora,
        b.Modulo,
        b.Accion,
        b.Entidad,
        b.IdEntidad,
        b.Resultado,
        b.Detalle,
        b.IP
        """;

    /// <summary>Origen de datos con el usuario proyectado; se usa tanto para contar como para listar.</summary>
    private const string OrigenEvento = """
        FROM dbo.Bitacora AS b
        LEFT JOIN dbo.Usuarios AS u ON u.IdUsuario = b.IdUsuario
        """;

    /// <summary>
    /// Filtros comunes a todas las consultas. El módulo de migración se excluye aquí, de modo que
    /// ninguna variante de la consulta pueda omitirlo.
    /// </summary>
    private const string FiltroComun = """
        WHERE (b.Modulo IS NULL OR b.Modulo <> @ModuloExcluido)
          AND (@Buscar IS NULL
               OR b.Modulo LIKE @Patron
               OR b.Accion LIKE @Patron
               OR b.Entidad LIKE @Patron
               OR b.Resultado LIKE @Patron
               OR b.Detalle LIKE @Patron
               OR u.Login LIKE @Patron
               OR u.Nombre LIKE @Patron)
          AND (@IdUsuario IS NULL OR b.IdUsuario = @IdUsuario)
          AND (@Modulo IS NULL OR b.Modulo = @Modulo)
          AND (@Accion IS NULL OR b.Accion = @Accion)
          AND (@Entidad IS NULL OR b.Entidad = @Entidad)
          AND (@Resultado IS NULL OR b.Resultado = @Resultado)
          AND (@FechaDesde IS NULL OR b.FechaHora >= @FechaDesde)
          AND (@FechaHasta IS NULL OR b.FechaHora <= @FechaHasta)
        """;

    /// <summary>Conteo total y página solicitada, resueltos en una sola ejecución.</summary>
    private static readonly string ConsultaPagina = $"""
        SELECT COUNT_BIG(*)
        {OrigenEvento}
        {FiltroComun};

        SELECT {ColumnasEvento}
        {OrigenEvento}
        {FiltroComun}
        ORDER BY b.FechaHora DESC, b.IdBitacora DESC
        OFFSET @Desplazamiento ROWS FETCH NEXT @TamanoRegistros ROWS ONLY;
        """;

    /// <summary>Valores distintos de los filtros, siempre sin el módulo de migración.</summary>
    private static readonly string ConsultaCatalogos = $"""
        SELECT DISTINCT b.Modulo
        FROM dbo.Bitacora AS b
        WHERE b.Modulo IS NOT NULL AND b.Modulo <> @ModuloExcluido
        ORDER BY b.Modulo;

        SELECT DISTINCT b.Accion
        FROM dbo.Bitacora AS b
        WHERE b.Accion IS NOT NULL AND (b.Modulo IS NULL OR b.Modulo <> @ModuloExcluido)
        ORDER BY b.Accion;

        SELECT DISTINCT b.Entidad
        FROM dbo.Bitacora AS b
        WHERE b.Entidad IS NOT NULL AND (b.Modulo IS NULL OR b.Modulo <> @ModuloExcluido)
        ORDER BY b.Entidad;

        SELECT DISTINCT b.Resultado
        FROM dbo.Bitacora AS b
        WHERE b.Resultado IS NOT NULL AND (b.Modulo IS NULL OR b.Modulo <> @ModuloExcluido)
        ORDER BY b.Resultado;

        SELECT DISTINCT u.IdUsuario, u.Login, u.Nombre
        FROM dbo.Bitacora AS b
        INNER JOIN dbo.Usuarios AS u ON u.IdUsuario = b.IdUsuario
        WHERE b.Modulo IS NULL OR b.Modulo <> @ModuloExcluido
        ORDER BY u.Nombre, u.Login, u.IdUsuario;
        """;

    /// <summary>Detalle de un evento; también excluye el módulo de migración.</summary>
    private static readonly string ConsultaPorId = $"""
        SELECT {ColumnasEvento}
        {OrigenEvento}
        WHERE b.IdBitacora = @IdBitacora
          AND (b.Modulo IS NULL OR b.Modulo <> @ModuloExcluido);
        """;

    private readonly SqlConnectionFactory _connectionFactory;

    /// <summary>
    /// Inicializa el repositorio con el proveedor de conexiones.
    /// </summary>
    /// <param name="connectionFactory">Proveedor de conexiones a la base de datos.</param>
    public BitacoraConsultaRepository(SqlConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    /// <inheritdoc />
    public async Task<BitacoraPaginaDto> ConsultarAsync(
        BitacoraConsultaDto consulta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var pagina = consulta.Pagina <= 0 ? BitacoraConsultaDto.PaginaPredeterminada : consulta.Pagina;
        var tamano = consulta.Tamano <= 0 ? BitacoraConsultaDto.TamanoPredeterminado : consulta.Tamano;

        await using var conexion = await _connectionFactory.AbrirAsync(cancellationToken);
        await using var comando = new SqlCommand(ConsultaPagina, conexion);

        AgregarParametrosFiltro(comando, consulta);
        comando.Parameters.Add("@Desplazamiento", SqlDbType.Int).Value = (pagina - 1) * tamano;
        comando.Parameters.Add("@TamanoRegistros", SqlDbType.Int).Value = tamano;

        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

        var total = await lector.ReadAsync(cancellationToken)
            ? Convert.ToInt64(lector.GetValue(0), CultureInfo.InvariantCulture)
            : 0L;

        var datos = new List<BitacoraItemWebDto>();

        if (await lector.NextResultAsync(cancellationToken))
        {
            while (await lector.ReadAsync(cancellationToken))
            {
                datos.Add(MapearEvento(lector));
            }
        }

        return new BitacoraPaginaDto
        {
            Pagina = pagina,
            Tamano = tamano,
            TotalRegistros = (int)Math.Min(total, int.MaxValue),
            Datos = datos
        };
    }

    /// <inheritdoc />
    public async Task<BitacoraCatalogosDto> ObtenerCatalogosAsync(CancellationToken cancellationToken = default)
    {
        await using var conexion = await _connectionFactory.AbrirAsync(cancellationToken);
        await using var comando = new SqlCommand(ConsultaCatalogos, conexion);
        comando.Parameters.Add("@ModuloExcluido", SqlDbType.NVarChar, LongitudModulo).Value = ModuloExcluido;

        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

        var modulos = await LeerTextosAsync(lector, cancellationToken);
        await lector.NextResultAsync(cancellationToken);
        var acciones = await LeerTextosAsync(lector, cancellationToken);
        await lector.NextResultAsync(cancellationToken);
        var entidades = await LeerTextosAsync(lector, cancellationToken);
        await lector.NextResultAsync(cancellationToken);
        var resultados = await LeerTextosAsync(lector, cancellationToken);

        var usuarios = new List<BitacoraUsuarioDto>();

        if (await lector.NextResultAsync(cancellationToken))
        {
            while (await lector.ReadAsync(cancellationToken))
            {
                usuarios.Add(new BitacoraUsuarioDto
                {
                    IdUsuario = lector.GetInt32(0),
                    Login = lector.IsDBNull(1) ? string.Empty : lector.GetString(1).Trim(),
                    Nombre = lector.IsDBNull(2) ? string.Empty : lector.GetString(2).Trim()
                });
            }
        }

        return new BitacoraCatalogosDto
        {
            Modulos = modulos,
            Acciones = acciones,
            Entidades = entidades,
            Resultados = resultados,
            Usuarios = usuarios
        };
    }

    /// <inheritdoc />
    public async Task<BitacoraItemWebDto?> ObtenerPorIdAsync(
        long idBitacora,
        CancellationToken cancellationToken = default)
    {
        if (idBitacora <= 0)
        {
            return null;
        }

        await using var conexion = await _connectionFactory.AbrirAsync(cancellationToken);
        await using var comando = new SqlCommand(ConsultaPorId, conexion);
        comando.Parameters.Add("@IdBitacora", SqlDbType.BigInt).Value = idBitacora;
        comando.Parameters.Add("@ModuloExcluido", SqlDbType.NVarChar, LongitudModulo).Value = ModuloExcluido;

        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

        return await lector.ReadAsync(cancellationToken) ? MapearEvento(lector) : null;
    }

    private static void AgregarParametrosFiltro(SqlCommand comando, BitacoraConsultaDto consulta)
    {
        // El módulo de migración se excluye siempre; no es un filtro opcional de la petición.
        comando.Parameters.Add("@ModuloExcluido", SqlDbType.NVarChar, LongitudModulo).Value = ModuloExcluido;

        comando.Parameters.Add("@IdUsuario", SqlDbType.Int).Value =
            consulta.IdUsuario is > 0 ? consulta.IdUsuario.Value : DBNull.Value;

        var buscar = NormalizarTexto(consulta.Buscar, LongitudBusqueda);
        comando.Parameters.Add("@Buscar", SqlDbType.NVarChar, LongitudBusqueda).Value = buscar ?? (object)DBNull.Value;
        comando.Parameters.Add("@Patron", SqlDbType.NVarChar, (LongitudBusqueda * 2) + 2).Value =
            buscar is null ? DBNull.Value : Patron(buscar);

        comando.Parameters.Add("@Modulo", SqlDbType.NVarChar, LongitudModulo).Value =
            NormalizarTexto(consulta.Modulo, LongitudModulo) ?? (object)DBNull.Value;
        comando.Parameters.Add("@Accion", SqlDbType.NVarChar, LongitudAccion).Value =
            NormalizarTexto(consulta.Accion, LongitudAccion) ?? (object)DBNull.Value;
        comando.Parameters.Add("@Entidad", SqlDbType.NVarChar, LongitudEntidad).Value =
            NormalizarTexto(consulta.Entidad, LongitudEntidad) ?? (object)DBNull.Value;
        comando.Parameters.Add("@Resultado", SqlDbType.NVarChar, LongitudResultado).Value =
            NormalizarTexto(consulta.Resultado, LongitudResultado) ?? (object)DBNull.Value;

        comando.Parameters.Add("@FechaDesde", SqlDbType.DateTime2).Value = consulta.FechaDesde ?? (object)DBNull.Value;
        comando.Parameters.Add("@FechaHasta", SqlDbType.DateTime2).Value = consulta.FechaHasta ?? (object)DBNull.Value;
    }

    /// <summary>
    /// Construye el patrón de <c>LIKE</c> escapando los comodines con corchetes: el texto buscado
    /// siempre se compara como literal.
    /// </summary>
    /// <param name="texto">Texto ya recortado.</param>
    private static string Patron(string texto) => "%" + texto
        .Replace("[", "[[]", StringComparison.Ordinal)
        .Replace("%", "[%]", StringComparison.Ordinal)
        .Replace("_", "[_]", StringComparison.Ordinal) + "%";

    private static string? NormalizarTexto(string? valor, int longitudMaxima)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var recortado = valor.Trim();

        return recortado.Length <= longitudMaxima ? recortado : recortado[..longitudMaxima];
    }

    private static async Task<IReadOnlyList<string>> LeerTextosAsync(
        SqlDataReader lector,
        CancellationToken cancellationToken)
    {
        var valores = new List<string>();

        while (await lector.ReadAsync(cancellationToken))
        {
            if (!lector.IsDBNull(0))
            {
                valores.Add(lector.GetString(0).Trim());
            }
        }

        return valores;
    }

    private static BitacoraItemWebDto MapearEvento(SqlDataReader lector)
    {
        var ordinalIdUsuario = lector.GetOrdinal("IdUsuario");
        var ordinalIdEntidad = lector.GetOrdinal("IdEntidad");

        return new BitacoraItemWebDto
        {
            IdBitacora = lector.GetInt64(lector.GetOrdinal("IdBitacora")),
            IdUsuario = lector.IsDBNull(ordinalIdUsuario) ? null : lector.GetInt32(ordinalIdUsuario),
            LoginUsuario = Texto(lector, "LoginUsuario"),
            NombreUsuario = Texto(lector, "NombreUsuario"),
            FechaHora = lector.GetDateTime(lector.GetOrdinal("FechaHora")),
            Modulo = Texto(lector, "Modulo") ?? string.Empty,
            Accion = Texto(lector, "Accion") ?? string.Empty,
            Entidad = Texto(lector, "Entidad") ?? string.Empty,
            IdEntidad = lector.IsDBNull(ordinalIdEntidad) ? null : lector.GetInt64(ordinalIdEntidad),
            Resultado = Texto(lector, "Resultado") ?? string.Empty,
            Detalle = Texto(lector, "Detalle") ?? string.Empty,
            Ip = Texto(lector, "IP")
        };
    }

    private static string? Texto(SqlDataReader lector, string columna)
    {
        var ordinal = lector.GetOrdinal(columna);

        return lector.IsDBNull(ordinal) ? null : lector.GetString(ordinal).Trim();
    }
}
