using VisorDatosSIG.Application.DTOs.Bitacora;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Infrastructure.Data;

/// <summary>
/// Servicio de consulta de la bitácora del sistema web (CU05 - Consultar Bitácora).
/// </summary>
/// <remarks>
/// Normaliza y acota los parámetros de entrada (página, tamaño de página, longitudes de los filtros
/// y rango de fechas) antes de delegar en el repositorio, de modo que ninguna consulta llegue a
/// SQL Server con valores fuera de rango. La exclusión del módulo de migración es responsabilidad
/// del repositorio y no puede desactivarse desde la petición.
/// </remarks>
public sealed class BitacoraConsultaService : IBitacoraConsultaService
{
    private readonly IBitacoraConsultaRepository _repositorio;

    /// <summary>
    /// Inicializa el servicio con el repositorio de consulta.
    /// </summary>
    /// <param name="repositorio">Origen de los eventos de bitácora.</param>
    public BitacoraConsultaService(IBitacoraConsultaRepository repositorio) =>
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));

    /// <inheritdoc />
    public Task<BitacoraPaginaDto> ConsultarAsync(
        BitacoraConsultaDto consulta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return _repositorio.ConsultarAsync(Normalizar(consulta), cancellationToken);
    }

    /// <inheritdoc />
    public Task<BitacoraCatalogosDto> ObtenerCatalogosAsync(CancellationToken cancellationToken = default) =>
        _repositorio.ObtenerCatalogosAsync(cancellationToken);

    /// <inheritdoc />
    public Task<BitacoraItemWebDto?> ObtenerPorIdAsync(
        long idBitacora,
        CancellationToken cancellationToken = default) =>
        idBitacora <= 0
            ? Task.FromResult<BitacoraItemWebDto?>(null)
            : _repositorio.ObtenerPorIdAsync(idBitacora, cancellationToken);

    /// <summary>
    /// Devuelve una copia normalizada de la consulta.
    /// </summary>
    /// <param name="consulta">Consulta solicitada por el cliente.</param>
    /// <returns>
    /// Consulta con página ≥ 1, tamaño entre 1 y <see cref="BitacoraConsultaDto.TamanoMaximo"/>,
    /// textos recortados (o <c>null</c> si quedan vacíos), usuario solo cuando es mayor que cero y
    /// el rango de fechas tal como lo envió el cliente.
    /// </returns>
    /// <remarks>
    /// Las fechas <b>no</b> se intercambian: un rango invertido lo rechaza la validación de
    /// <see cref="BitacoraConsultaDto"/> (<see cref="System.ComponentModel.DataAnnotations.IValidatableObject"/>)
    /// con HTTP 400 antes de llegar aquí, de modo que el servicio nunca reinterpreta en silencio
    /// la consulta solicitada.
    /// </remarks>
    public static BitacoraConsultaDto Normalizar(BitacoraConsultaDto consulta)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return new BitacoraConsultaDto
        {
            Pagina = Math.Max(BitacoraConsultaDto.PaginaPredeterminada, consulta.Pagina),
            Tamano = consulta.Tamano <= 0
                ? BitacoraConsultaDto.TamanoPredeterminado
                : Math.Min(consulta.Tamano, BitacoraConsultaDto.TamanoMaximo),
            Buscar = Texto(consulta.Buscar, BitacoraConsultaDto.LongitudMaximaBusqueda),
            IdUsuario = consulta.IdUsuario is > 0 ? consulta.IdUsuario : null,
            Modulo = Texto(consulta.Modulo, BitacoraConsultaDto.LongitudMaximaFiltro),
            Accion = Texto(consulta.Accion, BitacoraConsultaDto.LongitudMaximaFiltro),
            Entidad = Texto(consulta.Entidad, BitacoraConsultaDto.LongitudMaximaFiltro),
            Resultado = Texto(consulta.Resultado, BitacoraConsultaDto.LongitudMaximaFiltro),
            FechaDesde = consulta.FechaDesde,
            FechaHasta = consulta.FechaHasta
        };
    }

    private static string? Texto(string? valor, int longitudMaxima)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var recortado = valor.Trim();

        return recortado.Length <= longitudMaxima ? recortado : recortado[..longitudMaxima];
    }
}
