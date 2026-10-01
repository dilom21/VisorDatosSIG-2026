using System.ComponentModel.DataAnnotations;

namespace VisorDatosSIG.Application.DTOs.Bitacora;

/// <summary>
/// Filtros y paginación de la consulta de bitácora del sistema web (CU05 - Consultar Bitácora).
/// </summary>
/// <remarks>
/// Todos los filtros son opcionales y se aplican en SQL Server: la base de datos devuelve
/// únicamente la página solicitada, nunca el historial completo.
/// <para>
/// <b>Exclusión del migrador:</b> la consulta web nunca muestra los eventos del módulo de
/// migración. La exclusión es una regla del repositorio y no se puede desactivar desde la
/// petición, para no exponer operaciones técnicas fuera de su módulo.
/// </para>
/// </remarks>
public sealed class BitacoraConsultaDto : IValidatableObject
{
    /// <summary>Página predeterminada.</summary>
    public const int PaginaPredeterminada = 1;

    /// <summary>Tamaño de página predeterminado.</summary>
    public const int TamanoPredeterminado = 25;

    /// <summary>Tamaño de página máximo aceptado; los valores mayores se ajustan a este límite.</summary>
    public const int TamanoMaximo = 100;

    /// <summary>Longitud máxima de los filtros de texto exactos.</summary>
    public const int LongitudMaximaFiltro = 100;

    /// <summary>Longitud máxima del texto de búsqueda libre.</summary>
    public const int LongitudMaximaBusqueda = 200;

    /// <summary>Página solicitada, empezando en 1. Los valores menores se ajustan a 1.</summary>
    public int Pagina { get; set; } = PaginaPredeterminada;

    /// <summary>Tamaño de página; se ajusta al rango 1..100.</summary>
    public int Tamano { get; set; } = TamanoPredeterminado;

    /// <summary>Texto libre que se busca en módulo, acción, entidad, resultado, detalle y usuario.</summary>
    public string? Buscar { get; set; }

    /// <summary>Usuario que generó el evento.</summary>
    public int? IdUsuario { get; set; }

    /// <summary>Módulo exacto del sistema.</summary>
    public string? Modulo { get; set; }

    /// <summary>Acción exacta registrada.</summary>
    public string? Accion { get; set; }

    /// <summary>Entidad afectada.</summary>
    public string? Entidad { get; set; }

    /// <summary>Resultado del evento (por ejemplo EXITOSO o FALLIDO).</summary>
    public string? Resultado { get; set; }

    /// <summary>Fecha y hora mínima del evento (inclusive).</summary>
    public DateTime? FechaDesde { get; set; }

    /// <summary>Fecha y hora máxima del evento (inclusive).</summary>
    public DateTime? FechaHasta { get; set; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FechaDesde.HasValue && FechaHasta.HasValue && FechaDesde.Value > FechaHasta.Value)
        {
            yield return new ValidationResult(
                "La fecha 'desde' no puede ser posterior a la fecha 'hasta'.",
                [nameof(FechaDesde), nameof(FechaHasta)]);
        }
    }
}
