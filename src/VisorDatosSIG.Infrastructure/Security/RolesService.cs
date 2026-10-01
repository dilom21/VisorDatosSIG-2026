using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Navigation;
using VisorDatosSIG.Application.DTOs.Security;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Infrastructure.Security;

/// <summary>
/// Administración de roles y permisos del sistema web (CU03 - Gestionar Roles y CU04 - Gestionar
/// Permisos).
/// </summary>
/// <remarks>
/// Reglas aplicadas (ver <see cref="IRolesService"/>):
/// <list type="bullet">
/// <item>El rol responsable de la migración nunca se lista, consulta ni administra desde la web.</item>
/// <item><c>Administrador</c> es un rol del sistema: tiene acceso total implícito, por lo que no se
/// renombra, no se deshabilita y no se le editan permisos.</item>
/// <item>Un rol con usuarios activos asignados no puede deshabilitarse (se informa
/// <see cref="ResultadoRol.RolEnUso"/> en lugar de romper la integridad referencial).</item>
/// <item>Ningún rol se elimina: la baja es lógica (<c>dbo.Roles.Estado</c>), conservando la
/// trazabilidad de <c>dbo.Bitacora</c>.</item>
/// <item>Los nombres reservados y los duplicados se detectan antes de escribir, comparando sin
/// distinción de mayúsculas ni acentos.</item>
/// <item>El servicio no lanza excepciones de negocio: devuelve el resultado para que el controlador
/// elija el código HTTP.</item>
/// </list>
/// La matriz de permisos se arma con las opciones <b>activas</b> de <c>dbo.MenuOpciones</c>, de modo
/// que la pantalla de permisos y el menú lateral siempre muestren el mismo catálogo.
/// </remarks>
public sealed class RolesService : IRolesService
{
    private const int EstadoActivo = 1;

    private readonly IRolRepository _rolRepository;
    private readonly IMenuRepository _menuRepository;

    /// <summary>
    /// Inicializa el servicio con el repositorio de roles y el catálogo del menú.
    /// </summary>
    /// <param name="rolRepository">Persistencia de roles y de la matriz de permisos.</param>
    /// <param name="menuRepository">Origen del catálogo de opciones de menú.</param>
    public RolesService(IRolRepository rolRepository, IMenuRepository menuRepository)
    {
        _rolRepository = rolRepository ?? throw new ArgumentNullException(nameof(rolRepository));
        _menuRepository = menuRepository ?? throw new ArgumentNullException(nameof(menuRepository));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RolDto>> ObtenerRolesAsync(CancellationToken cancellationToken = default)
    {
        var filas = await _rolRepository.ObtenerRolesAsync(cancellationToken);

        return filas
            .Where(fila => fila.IdRol > 0 && EsAdministrable(fila.NombreRol))
            .Select(fila => fila.AContrato())
            .ToList();
    }

    /// <inheritdoc />
    public async Task<RolDto?> ObtenerRolAsync(int idRol, CancellationToken cancellationToken = default)
    {
        if (idRol <= 0)
        {
            return null;
        }

        var fila = await _rolRepository.ObtenerRolPorIdAsync(idRol, cancellationToken);

        return fila is null || !EsAdministrable(fila.NombreRol) ? null : fila.AContrato();
    }

    /// <inheritdoc />
    public async Task<RolOperacionDto> CrearRolAsync(
        RolSolicitudDto solicitud,
        ContextoAuditoriaDto auditoria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var datosInvalidos = Validar(solicitud);

        if (datosInvalidos is not null)
        {
            return datosInvalidos;
        }

        var nombreRol = solicitud.NombreRol.Trim();

        if (RolesSistema.EsNombreReservado(nombreRol))
        {
            return RolOperacionDto.Fallido(
                ResultadoRol.NombreReservado,
                $"El nombre '{nombreRol}' está reservado por el sistema y no puede asignarse a un rol nuevo.");
        }

        if (await _rolRepository.ExisteNombreRolAsync(nombreRol, null, cancellationToken))
        {
            return RolOperacionDto.Fallido(
                ResultadoRol.NombreDuplicado,
                $"Ya existe un rol con el nombre '{nombreRol}'.");
        }

        var descripcion = NormalizarDescripcion(solicitud.Descripcion);

        var idRol = await _rolRepository.CrearRolAsync(
            nombreRol,
            descripcion,
            idGenerado => BitacoraEventosSeguridad.Evento(
                auditoria,
                BitacoraEventosSeguridad.AccionCrearRol,
                BitacoraEventosSeguridad.EntidadRol,
                idGenerado,
                $"Rol '{nombreRol}' creado."),
            cancellationToken);

        return RolOperacionDto.Exitoso(new RolDto
        {
            IdRol = idRol,
            NombreRol = nombreRol,
            Descripcion = descripcion,
            Activo = true,
            EsRolSistema = false,
            CantidadUsuariosActivos = 0
        });
    }

    /// <inheritdoc />
    public async Task<RolOperacionDto> ActualizarRolAsync(
        int idRol,
        RolSolicitudDto solicitud,
        ContextoAuditoriaDto auditoria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var actual = await _rolRepository.ObtenerRolPorIdAsync(idRol, cancellationToken);

        if (actual is null || !EsAdministrable(actual.NombreRol))
        {
            return RolOperacionDto.Fallido(ResultadoRol.NoEncontrado, "El rol indicado no existe o no se administra desde la web.");
        }

        if (RolesSistema.EsAdministrador(actual.NombreRol))
        {
            return RolOperacionDto.Fallido(
                ResultadoRol.RolProtegido,
                $"El rol '{actual.NombreRol}' es un rol del sistema y no puede modificarse.");
        }

        var datosInvalidos = Validar(solicitud);

        if (datosInvalidos is not null)
        {
            return datosInvalidos;
        }

        var nombreRol = solicitud.NombreRol.Trim();

        if (RolesSistema.EsNombreReservado(nombreRol))
        {
            return RolOperacionDto.Fallido(
                ResultadoRol.NombreReservado,
                $"El nombre '{nombreRol}' está reservado por el sistema.");
        }

        if (await _rolRepository.ExisteNombreRolAsync(nombreRol, idRol, cancellationToken))
        {
            return RolOperacionDto.Fallido(
                ResultadoRol.NombreDuplicado,
                $"Ya existe otro rol con el nombre '{nombreRol}'.");
        }

        var descripcion = NormalizarDescripcion(solicitud.Descripcion);

        var auditoriaRol = BitacoraEventosSeguridad.Evento(
            auditoria,
            BitacoraEventosSeguridad.AccionModificarRol,
            BitacoraEventosSeguridad.EntidadRol,
            idRol,
            DetalleModificacion(actual, nombreRol, descripcion));

        if (!await _rolRepository.ActualizarRolAsync(idRol, nombreRol, descripcion, auditoriaRol, cancellationToken))
        {
            return RolOperacionDto.Fallido(ResultadoRol.NoEncontrado, "El rol indicado dejó de existir.");
        }

        return RolOperacionDto.Exitoso(new RolDto
        {
            IdRol = idRol,
            NombreRol = nombreRol,
            Descripcion = descripcion,
            Activo = actual.Activo,
            EsRolSistema = RolesSistema.EsAdministrador(nombreRol),
            CantidadUsuariosActivos = actual.CantidadUsuariosActivos
        });
    }

    /// <summary>
    /// Redacta el detalle de auditoría de una modificación, indicando qué cambió.
    /// </summary>
    /// <param name="actual">Estado previo del rol.</param>
    /// <param name="nombreRol">Nombre nuevo.</param>
    /// <param name="descripcion">Descripción nueva.</param>
    private static string DetalleModificacion(RolFilaDto actual, string nombreRol, string? descripcion)
    {
        var cambios = new List<string>();

        if (!string.Equals(actual.NombreRol, nombreRol, StringComparison.Ordinal))
        {
            cambios.Add($"nombre de '{actual.NombreRol}' a '{nombreRol}'");
        }

        if (!string.Equals(actual.Descripcion, descripcion, StringComparison.Ordinal))
        {
            cambios.Add("descripción");
        }

        return cambios.Count == 0
            ? $"Rol '{nombreRol}' actualizado sin cambios de valores."
            : $"Rol '{actual.NombreRol}' actualizado: {string.Join(", ", cambios)}.";
    }

    /// <inheritdoc />
    public async Task<RolOperacionDto> CambiarEstadoAsync(
        int idRol,
        bool activo,
        ContextoAuditoriaDto auditoria,
        CancellationToken cancellationToken = default)
    {
        var actual = await _rolRepository.ObtenerRolPorIdAsync(idRol, cancellationToken);

        if (actual is null || !EsAdministrable(actual.NombreRol))
        {
            return RolOperacionDto.Fallido(ResultadoRol.NoEncontrado, "El rol indicado no existe o no se administra desde la web.");
        }

        if (RolesSistema.EsAdministrador(actual.NombreRol))
        {
            return RolOperacionDto.Fallido(
                ResultadoRol.RolProtegido,
                $"El rol '{actual.NombreRol}' es un rol del sistema y no puede deshabilitarse.");
        }

        // Repetir el estado actual no es un error: la operación es idempotente y no registra auditoría.
        if (actual.Activo == activo)
        {
            return RolOperacionDto.Exitoso(actual.AContrato());
        }

        if (!activo && actual.CantidadUsuariosActivos > 0)
        {
            return RolOperacionDto.Fallido(
                ResultadoRol.RolEnUso,
                $"El rol '{actual.NombreRol}' tiene {actual.CantidadUsuariosActivos} usuario(s) activo(s) asignado(s): " +
                "reasígnelos antes de deshabilitarlo.");
        }

        var auditoriaEstado = BitacoraEventosSeguridad.Evento(
            auditoria,
            BitacoraEventosSeguridad.AccionCambiarEstadoRol,
            BitacoraEventosSeguridad.EntidadRol,
            idRol,
            $"Rol '{actual.NombreRol}' {(activo ? "habilitado" : "deshabilitado")}.");

        if (!await _rolRepository.ActualizarEstadoRolAsync(idRol, activo, auditoriaEstado, cancellationToken))
        {
            return RolOperacionDto.Fallido(ResultadoRol.NoEncontrado, "El rol indicado dejó de existir.");
        }

        return RolOperacionDto.Exitoso(new RolDto
        {
            IdRol = actual.IdRol,
            NombreRol = actual.NombreRol,
            Descripcion = actual.Descripcion,
            Activo = activo,
            EsRolSistema = RolesSistema.EsAdministrador(actual.NombreRol),
            CantidadUsuariosActivos = actual.CantidadUsuariosActivos
        });
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CatalogoPermisoDto>> ObtenerCatalogoPermisosAsync(
        CancellationToken cancellationToken = default) =>
        ConstruirCatalogo(await _menuRepository.ObtenerOpcionesAsync(cancellationToken));

    /// <inheritdoc />
    public async Task<RolPermisosDto?> ObtenerPermisosAsync(int idRol, CancellationToken cancellationToken = default)
    {
        var rol = await _rolRepository.ObtenerRolPorIdAsync(idRol, cancellationToken);

        if (rol is null || !EsAdministrable(rol.NombreRol))
        {
            return null;
        }

        var vigentes = (await _rolRepository.ObtenerPermisosRolAsync(idRol, cancellationToken))
            .Where(permiso => permiso.IdMenu > 0)
            .GroupBy(permiso => permiso.IdMenu)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Last());

        var menus = await _menuRepository.ObtenerOpcionesAsync(cancellationToken);

        return ConstruirMatriz(rol, menus, vigentes);
    }

    /// <inheritdoc />
    public async Task<RolPermisosOperacionDto> GuardarPermisosAsync(
        int idRol,
        RolPermisosSolicitudDto solicitud,
        ContextoAuditoriaDto auditoria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var rol = await _rolRepository.ObtenerRolPorIdAsync(idRol, cancellationToken);

        if (rol is null || !EsAdministrable(rol.NombreRol))
        {
            return RolPermisosOperacionDto.Fallido(ResultadoRol.NoEncontrado, "El rol indicado no existe o no se administra desde la web.");
        }

        if (RolesSistema.EsAdministrador(rol.NombreRol))
        {
            return RolPermisosOperacionDto.Fallido(
                ResultadoRol.RolProtegido,
                $"El rol '{rol.NombreRol}' tiene acceso total implícito: sus permisos no se editan.");
        }

        if (solicitud.Permisos is null)
        {
            return RolPermisosOperacionDto.Fallido(ResultadoRol.DatosInvalidos, "La lista de permisos es obligatoria.");
        }

        var permisos = solicitud.Permisos
            .Where(permiso => permiso is not null)
            .Select(permiso => new PermisoMenuFilaDto
            {
                IdMenu = permiso.IdMenu,
                PuedeVer = permiso.PuedeVer,
                PuedeCrear = permiso.PuedeCrear,
                PuedeEditar = permiso.PuedeEditar,
                PuedeEliminar = permiso.PuedeEliminar
            })
            .ToList();

        var idsMenu = permisos
            .Select(permiso => permiso.IdMenu)
            .Distinct()
            .OrderBy(idMenu => idMenu)
            .ToList();

        if (idsMenu.Exists(idMenu => idMenu <= 0))
        {
            return RolPermisosOperacionDto.Fallido(
                ResultadoRol.DatosInvalidos,
                "Cada permiso debe indicar un identificador de menú mayor que cero.");
        }

        if (idsMenu.Count > 0 && !await _rolRepository.ExistenMenusActivosAsync(idsMenu, cancellationToken))
        {
            return RolPermisosOperacionDto.Fallido(
                ResultadoRol.DatosInvalidos,
                "La matriz incluye opciones de menú que no existen o están inactivas.");
        }

        var conPermisos = permisos.Where(permiso => permiso.TieneAlgunPermiso).ToList();

        var auditoriaPermisos = BitacoraEventosSeguridad.Evento(
            auditoria,
            BitacoraEventosSeguridad.AccionActualizarPermisos,
            BitacoraEventosSeguridad.EntidadPermisoRol,
            idRol,
            $"Matriz de permisos del rol '{rol.NombreRol}' reemplazada: {conPermisos.Count} opción(es) con al menos un permiso.");

        // Solo se persisten las filas que otorgan algún permiso: su ausencia significa lo mismo.
        if (!await _rolRepository.GuardarPermisosRolAsync(idRol, conPermisos, auditoriaPermisos, cancellationToken))
        {
            return RolPermisosOperacionDto.Fallido(ResultadoRol.NoEncontrado, "El rol indicado dejó de existir.");
        }

        var vigentes = await ObtenerPermisosAsync(idRol, cancellationToken);

        return vigentes is null
            ? RolPermisosOperacionDto.Fallido(ResultadoRol.NoEncontrado, "El rol indicado dejó de existir.")
            : RolPermisosOperacionDto.Exitoso(vigentes);
    }

    /// <summary>
    /// Construye el catálogo de permisos (árbol) con las opciones activas del menú.
    /// </summary>
    /// <param name="filas">Filas de <c>dbo.MenuOpciones</c> en cualquier orden.</param>
    /// <returns>Opciones de primer nivel con sus hijas anidadas y ordenadas.</returns>
    /// <remarks>
    /// La relación padre/hijo se resuelve solo por <c>IdMenuPadre</c> y el orden por <c>Orden</c> y
    /// <c>IdMenu</c>, igual que el menú lateral: la pantalla de permisos ofrece exactamente las
    /// opciones que el sistema puede mostrar.
    /// </remarks>
    public static IReadOnlyList<CatalogoPermisoDto> ConstruirCatalogo(IEnumerable<MenuOpcionPlanaDto>? filas)
    {
        var activas = OpcionesActivas(filas);
        var hijosPorPadre = activas.Where(fila => fila.IdMenuPadre.HasValue).ToLookup(fila => fila.IdMenuPadre!.Value);
        var visitados = new HashSet<int>();

        return activas
            .Where(fila => !fila.IdMenuPadre.HasValue)
            .Select(fila => ConstruirNodo(fila, hijosPorPadre, visitados))
            .OfType<CatalogoPermisoDto>()
            .ToList();
    }

    /// <summary>
    /// Proyecta la matriz de permisos de un rol: una fila por opción activa, en el orden del menú
    /// lateral, con los permisos vigentes de <c>dbo.RolMenu</c>.
    /// </summary>
    /// <param name="rol">Rol al que pertenece la matriz.</param>
    /// <param name="menus">Opciones del menú en cualquier orden.</param>
    /// <param name="permisosVigentes">Permisos almacenados, indexados por identificador de menú.</param>
    /// <returns>
    /// Matriz con una fila por opción activa. El rol <c>Administrador</c> se informa con los cuatro
    /// permisos concedidos porque su acceso total es implícito.
    /// </returns>
    public static RolPermisosDto ConstruirMatriz(
        RolFilaDto rol,
        IEnumerable<MenuOpcionPlanaDto>? menus,
        IReadOnlyDictionary<int, PermisoMenuFilaDto>? permisosVigentes)
    {
        ArgumentNullException.ThrowIfNull(rol);

        var vigentes = permisosVigentes ?? new Dictionary<int, PermisoMenuFilaDto>();
        var accesoTotal = RolesSistema.EsAdministrador(rol.NombreRol);

        var filas = AplanarMenus(menus)
            .Select(fila =>
            {
                vigentes.TryGetValue(fila.IdMenu, out var permiso);

                return new MenuPermisoDto
                {
                    IdMenu = fila.IdMenu,
                    IdMenuPadre = fila.IdMenuPadre,
                    NombreMenu = fila.NombreMenu,
                    Url = NormalizarTexto(fila.Url),
                    Icono = NormalizarTexto(fila.Icono),
                    Orden = fila.Orden,
                    EsAgrupador = string.IsNullOrWhiteSpace(fila.Url),
                    PuedeVer = accesoTotal || permiso?.PuedeVer == true,
                    PuedeCrear = accesoTotal || permiso?.PuedeCrear == true,
                    PuedeEditar = accesoTotal || permiso?.PuedeEditar == true,
                    PuedeEliminar = accesoTotal || permiso?.PuedeEliminar == true
                };
            })
            .ToList();

        return new RolPermisosDto
        {
            IdRol = rol.IdRol,
            NombreRol = rol.NombreRol,
            EsRolSistema = accesoTotal,
            Menus = filas
        };
    }

    /// <summary>
    /// Aplana las opciones activas en el orden del menú lateral: cada opción seguida de sus hijas.
    /// </summary>
    /// <param name="filas">Opciones del menú en cualquier orden.</param>
    /// <returns>Opciones activas en orden jerárquico.</returns>
    public static IReadOnlyList<MenuOpcionPlanaDto> AplanarMenus(IEnumerable<MenuOpcionPlanaDto>? filas)
    {
        var activas = OpcionesActivas(filas);
        var hijosPorPadre = activas.Where(fila => fila.IdMenuPadre.HasValue).ToLookup(fila => fila.IdMenuPadre!.Value);
        var visitados = new HashSet<int>();
        var resultado = new List<MenuOpcionPlanaDto>();

        foreach (var raiz in activas.Where(fila => !fila.IdMenuPadre.HasValue))
        {
            AgregarAplanado(raiz, hijosPorPadre, visitados, resultado);
        }

        return resultado;
    }

    /// <summary>
    /// Indica si un nombre de rol participa de la administración web (el rol del migrador queda
    /// fuera).
    /// </summary>
    /// <param name="nombreRol">Nombre del rol tal como está almacenado.</param>
    public static bool EsAdministrable(string? nombreRol) => !RolesSistema.EsResponsableMigrador(nombreRol);

    private static RolOperacionDto? Validar(RolSolicitudDto solicitud)
    {
        if (!RolesSistema.EsNombreValido(solicitud.NombreRol))
        {
            return RolOperacionDto.Fallido(
                ResultadoRol.DatosInvalidos,
                $"El nombre del rol es obligatorio y no puede superar los {RolesSistema.LongitudMaximaNombre} caracteres.");
        }

        if (solicitud.Descripcion is { } descripcion
            && descripcion.Trim().Length > RolesSistema.LongitudMaximaDescripcion)
        {
            return RolOperacionDto.Fallido(
                ResultadoRol.DatosInvalidos,
                $"La descripción no puede superar los {RolesSistema.LongitudMaximaDescripcion} caracteres.");
        }

        return null;
    }

    private static string? NormalizarDescripcion(string? descripcion)
    {
        if (string.IsNullOrWhiteSpace(descripcion))
        {
            return null;
        }

        var recortada = descripcion.Trim();

        return recortada.Length <= RolesSistema.LongitudMaximaDescripcion
            ? recortada
            : recortada[..RolesSistema.LongitudMaximaDescripcion];
    }

    private static List<MenuOpcionPlanaDto> OpcionesActivas(IEnumerable<MenuOpcionPlanaDto>? filas) =>
        Ordenar(filas ?? [])
            .Where(fila => fila is not null && fila.Estado == EstadoActivo && fila.IdMenu > 0)
            .ToList();

    private static IEnumerable<MenuOpcionPlanaDto> Ordenar(IEnumerable<MenuOpcionPlanaDto> filas) =>
        filas.OrderBy(fila => fila.Orden).ThenBy(fila => fila.IdMenu);

    private static CatalogoPermisoDto? ConstruirNodo(
        MenuOpcionPlanaDto fila,
        ILookup<int, MenuOpcionPlanaDto> hijosPorPadre,
        HashSet<int> visitados)
    {
        // El registro de visitados corta referencias circulares o identificadores repetidos.
        if (!visitados.Add(fila.IdMenu))
        {
            return null;
        }

        var hijos = Ordenar(hijosPorPadre[fila.IdMenu])
            .Select(hijo => ConstruirNodo(hijo, hijosPorPadre, visitados))
            .OfType<CatalogoPermisoDto>()
            .ToList();

        return new CatalogoPermisoDto
        {
            IdMenu = fila.IdMenu,
            NombreMenu = fila.NombreMenu,
            Url = NormalizarTexto(fila.Url),
            Icono = NormalizarTexto(fila.Icono),
            Orden = fila.Orden,
            Opciones = hijos
        };
    }

    private static void AgregarAplanado(
        MenuOpcionPlanaDto fila,
        ILookup<int, MenuOpcionPlanaDto> hijosPorPadre,
        HashSet<int> visitados,
        List<MenuOpcionPlanaDto> resultado)
    {
        if (!visitados.Add(fila.IdMenu))
        {
            return;
        }

        resultado.Add(fila);

        foreach (var hijo in Ordenar(hijosPorPadre[fila.IdMenu]))
        {
            AgregarAplanado(hijo, hijosPorPadre, visitados, resultado);
        }
    }

    private static string? NormalizarTexto(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
