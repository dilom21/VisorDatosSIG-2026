using VisorDatosSIG.Application.DTOs.Navigation;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Infrastructure.Navigation;

/// <summary>
/// Servicio del menú lateral dinámico: transforma las filas planas de
/// <c>dbo.MenuOpciones</c> en la jerarquía padre → hijos que consume el sidebar, filtrada por los
/// permisos efectivos del usuario autenticado.
/// </summary>
/// <remarks>
/// Reglas aplicadas:
/// <list type="bullet">
/// <item>Un menú es padre (agrupador) cuando su <c>Url</c> es nula; la relación con sus hijos se
/// resuelve exclusivamente por <c>IdMenuPadre</c> (nunca por nombre ni por identificadores fijos).</item>
/// <item>Los padres se ordenan por <c>Orden</c> y, ante empate, por <c>IdMenu</c>; cada conjunto de
/// hijos usa el mismo criterio.</item>
/// <item>Solo se incluyen opciones activas (<c>Estado = 1</c>). El repositorio ya filtra en el
/// origen; el filtro se repite aquí como defensa y para que la regla sea verificable sin conexión
/// a la base de datos.</item>
/// <item>Un hijo cuya opción padre no está activa se descarta: no se promueve a primer nivel.</item>
/// <item>Se incluye una opción con ruta únicamente si alguno de los roles web del usuario puede
/// verla, y un agrupador solo si le queda al menos un hijo visible (nunca aparece un menú vacío).</item>
/// <item>Quien tenga acceso total (<c>Administrador</c>) recibe todas las opciones activas.</item>
/// </list>
/// Los permisos se leen en cada petición (ver <see cref="IPermisoService"/>), de modo que un
/// cambio en la matriz de permisos de un rol afecta el menú del usuario sin volver a iniciar sesión.
/// </remarks>
public sealed class MenuService : IMenuService
{
    private const int EstadoActivo = 1;

    private readonly IMenuRepository _menuRepository;
    private readonly IPermisoService _permisoService;

    /// <summary>
    /// Inicializa el servicio con el repositorio del menú y el resolutor de permisos.
    /// </summary>
    /// <param name="menuRepository">Origen de las opciones de menú.</param>
    /// <param name="permisoService">Resolutor de los permisos efectivos del usuario.</param>
    public MenuService(IMenuRepository menuRepository, IPermisoService permisoService)
    {
        _menuRepository = menuRepository ?? throw new ArgumentNullException(nameof(menuRepository));
        _permisoService = permisoService ?? throw new ArgumentNullException(nameof(permisoService));
    }

    /// <inheritdoc />
    public async Task<MenuUsuarioDto> ObtenerMenuAsync(int idUsuario, CancellationToken cancellationToken = default)
    {
        var permisos = await _permisoService.ObtenerPermisosAsync(idUsuario, cancellationToken);

        if (!permisos.TieneAccesoWeb)
        {
            // Un usuario que solo tenga el rol del migrador no recibe opciones: el controlador
            // traduce esta marca a 403 en lugar de devolver un menú vacío.
            return MenuUsuarioDto.SinAccesoWeb();
        }

        var filas = await _menuRepository.ObtenerOpcionesAsync(cancellationToken);
        var opciones = ConstruirJerarquia(filas, permisos.AccesoTotal ? null : permisos.MenusVisibles);

        return new MenuUsuarioDto
        {
            TieneAccesoWeb = true,
            Opciones = opciones
        };
    }

    /// <summary>
    /// Construye la jerarquía de menú con todas las opciones activas, sin filtrar por permisos.
    /// </summary>
    /// <param name="filas">Filas del menú en cualquier orden.</param>
    /// <returns>Menús de primer nivel con sus hijas anidadas y ordenadas.</returns>
    public static IReadOnlyList<MenuOpcionDto> ConstruirJerarquia(IEnumerable<MenuOpcionPlanaDto>? filas) =>
        ConstruirJerarquia(filas, null);

    /// <summary>
    /// Construye la jerarquía de menú a partir de las filas planas, filtrando por permisos.
    /// </summary>
    /// <param name="filas">Filas del menú en cualquier orden.</param>
    /// <param name="menusVisibles">
    /// Identificadores de los menús que el usuario puede ver; <c>null</c> cuando tiene acceso total
    /// y no corresponde filtrar.
    /// </param>
    /// <returns>
    /// Menús de primer nivel con sus hijas anidadas y ordenadas. Una opción con ruta se incluye
    /// solo si está en <paramref name="menusVisibles"/>; un agrupador se incluye solo cuando le
    /// queda al menos un hijo visible.
    /// </returns>
    public static IReadOnlyList<MenuOpcionDto> ConstruirJerarquia(
        IEnumerable<MenuOpcionPlanaDto>? filas,
        IReadOnlySet<int>? menusVisibles)
    {
        var activas = (filas ?? [])
            .Where(fila => fila is not null && fila.Estado == EstadoActivo && fila.IdMenu > 0)
            .ToList();

        if (activas.Count == 0)
        {
            return [];
        }

        var hijosPorPadre = activas
            .Where(fila => fila.IdMenuPadre.HasValue)
            .ToLookup(fila => fila.IdMenuPadre!.Value);

        // Un hijo cuyo padre no está activo queda huérfano respecto del menú visible
        // y por diseño no se muestra: solo los IdMenuPadre nulos son de primer nivel.
        var visitados = new HashSet<int>();

        return activas
            .Where(fila => !fila.IdMenuPadre.HasValue)
            .OrderBy(fila => fila.Orden)
            .ThenBy(fila => fila.IdMenu)
            .Select(fila => Construir(fila, hijosPorPadre, visitados, menusVisibles))
            .OfType<MenuOpcionDto>()
            .ToList();
    }

    private static MenuOpcionDto? Construir(
        MenuOpcionPlanaDto fila,
        ILookup<int, MenuOpcionPlanaDto> hijosPorPadre,
        HashSet<int> visitados,
        IReadOnlySet<int>? menusVisibles)
    {
        // El registro de visitados corta referencias circulares o identificadores repetidos.
        visitados.Add(fila.IdMenu);

        var hijos = hijosPorPadre[fila.IdMenu]
            .Where(hijo => !visitados.Contains(hijo.IdMenu))
            .OrderBy(hijo => hijo.Orden)
            .ThenBy(hijo => hijo.IdMenu)
            .Select(hijo => Construir(hijo, hijosPorPadre, visitados, menusVisibles))
            .OfType<MenuOpcionDto>()
            .ToList();

        var esAgrupador = string.IsNullOrWhiteSpace(fila.Url);

        if (menusVisibles is not null)
        {
            // Un agrupador no autoriza nada por sí mismo: se muestra mientras conserve al menos
            // un hijo visible. Una opción con ruta requiere su propio permiso de ver.
            if (esAgrupador ? hijos.Count == 0 : !menusVisibles.Contains(fila.IdMenu))
            {
                return null;
            }
        }

        return new MenuOpcionDto
        {
            IdMenu = fila.IdMenu,
            NombreMenu = fila.NombreMenu,
            Url = NormalizarTexto(fila.Url),
            Icono = NormalizarTexto(fila.Icono),
            Orden = fila.Orden,
            Opciones = hijos
        };
    }

    private static string? NormalizarTexto(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
