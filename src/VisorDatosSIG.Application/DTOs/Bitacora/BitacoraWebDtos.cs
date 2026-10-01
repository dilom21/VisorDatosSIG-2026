namespace VisorDatosSIG.Application.DTOs.Bitacora;

/// <summary>
/// Evento de bitácora consultable desde el sistema web.
/// </summary>
/// <remarks>
/// Incluye la proyección del usuario (<c>Login</c> y <c>Nombre</c>) resuelta en la misma consulta
/// mediante <c>LEFT JOIN</c>: no hay consultas N+1. Los eventos sin usuario (por ejemplo intentos
/// de inicio de sesión con un login inexistente) devuelven los campos de usuario en <c>null</c>.
/// Solo contiene datos de auditoría: nunca contraseñas, hashes ni tokens.
/// </remarks>
public sealed record BitacoraItemWebDto
{
    /// <summary>Identificador del evento.</summary>
    public long IdBitacora { get; init; }

    /// <summary>Identificador del usuario; <c>null</c> cuando el evento no tiene usuario asociado.</summary>
    public int? IdUsuario { get; init; }

    /// <summary>Login del usuario; <c>null</c> cuando no se pudo resolver.</summary>
    public string? LoginUsuario { get; init; }

    /// <summary>Nombre del usuario; <c>null</c> cuando no se pudo resolver.</summary>
    public string? NombreUsuario { get; init; }

    /// <summary>Fecha y hora del evento.</summary>
    public DateTime FechaHora { get; init; }

    /// <summary>Módulo del sistema.</summary>
    public string Modulo { get; init; } = string.Empty;

    /// <summary>Acción registrada.</summary>
    public string Accion { get; init; } = string.Empty;

    /// <summary>Entidad afectada.</summary>
    public string Entidad { get; init; } = string.Empty;

    /// <summary>Identificador de la entidad afectada.</summary>
    public long? IdEntidad { get; init; }

    /// <summary>Resultado del evento.</summary>
    public string Resultado { get; init; } = string.Empty;

    /// <summary>Detalle legible del evento.</summary>
    public string Detalle { get; init; } = string.Empty;

    /// <summary>Dirección IP de origen.</summary>
    public string? Ip { get; init; }
}

/// <summary>
/// Página de resultados de la bitácora web.
/// </summary>
/// <remarks>
/// El orden es determinista: <c>FechaHora DESC</c> y, ante empates, <c>IdBitacora DESC</c>.
/// <see cref="TotalPaginas"/> se calcula a partir de <see cref="TotalRegistros"/> y
/// <see cref="Tamano"/>.
/// </remarks>
public sealed record BitacoraPaginaDto
{
    /// <summary>Página devuelta, empezando en 1.</summary>
    public int Pagina { get; init; } = BitacoraConsultaDto.PaginaPredeterminada;

    /// <summary>Cantidad de elementos por página aplicada.</summary>
    public int Tamano { get; init; } = BitacoraConsultaDto.TamanoPredeterminado;

    /// <summary>Cantidad total de eventos que cumplen los filtros.</summary>
    public int TotalRegistros { get; init; }

    /// <summary>Cantidad total de páginas para los filtros aplicados.</summary>
    public int TotalPaginas => Tamano <= 0
        ? 0
        : (int)Math.Ceiling(TotalRegistros / (double)Tamano);

    /// <summary>Eventos de la página solicitada.</summary>
    public IReadOnlyList<BitacoraItemWebDto> Datos { get; init; } = [];
}

/// <summary>
/// Valores disponibles para los filtros de la bitácora web.
/// </summary>
/// <remarks>
/// Contrato de <c>GET /api/bitacora/catalogos</c>. Se obtiene de los propios eventos (valores
/// distintos en la base de datos) y también excluye el módulo de migración, de modo que los
/// combos del formulario nunca ofrecen valores que la consulta no pueda devolver.
/// </remarks>
public sealed record BitacoraCatalogosDto
{
    /// <summary>Módulos con eventos registrados.</summary>
    public IReadOnlyList<string> Modulos { get; init; } = [];

    /// <summary>Acciones registradas.</summary>
    public IReadOnlyList<string> Acciones { get; init; } = [];

    /// <summary>Entidades afectadas registradas.</summary>
    public IReadOnlyList<string> Entidades { get; init; } = [];

    /// <summary>Resultados registrados.</summary>
    public IReadOnlyList<string> Resultados { get; init; } = [];

    /// <summary>Usuarios que han generado eventos, para filtrar por usuario.</summary>
    public IReadOnlyList<BitacoraUsuarioDto> Usuarios { get; init; } = [];
}

/// <summary>
/// Usuario que aparece en los eventos de la bitácora web.
/// </summary>
/// <remarks>
/// Solo expone identificador, login y nombre: nunca el hash de la contraseña, el estado ni las
/// fechas internas de <c>dbo.Usuarios</c>.
/// </remarks>
public sealed record BitacoraUsuarioDto
{
    /// <summary>Identificador del usuario.</summary>
    public int IdUsuario { get; init; }

    /// <summary>Login del usuario.</summary>
    public string Login { get; init; } = string.Empty;

    /// <summary>Nombre del usuario.</summary>
    public string Nombre { get; init; } = string.Empty;
}
