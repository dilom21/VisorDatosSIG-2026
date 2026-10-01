using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Security;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Infrastructure.Security;

/// <summary>
/// Resuelve los permisos efectivos del usuario autenticado combinando sus roles activos.
/// </summary>
/// <remarks>
/// Reglas aplicadas (ver <see cref="IPermisoService"/>):
/// <list type="number">
/// <item>El rol del migrador se descarta: no otorga acceso al visor web.</item>
/// <item>El rol <c>Administrador</c> otorga acceso total implícito, sin consultar
/// <c>dbo.RolMenu</c>.</item>
/// <item>Los demás roles se unen (acumulativo): el permiso existe si al menos un rol lo otorga.</item>
/// </list>
/// Los permisos se leen en cada petición, por lo que un cambio en la matriz de un rol se refleja
/// en la siguiente petición del usuario afectado sin obligarlo a iniciar sesión de nuevo.
/// </remarks>
public sealed class PermisoService : IPermisoService
{
    private readonly IPermisoRepository _permisoRepository;

    /// <summary>
    /// Inicializa el servicio con el repositorio de permisos.
    /// </summary>
    /// <param name="permisoRepository">Origen de los roles y permisos almacenados.</param>
    public PermisoService(IPermisoRepository permisoRepository) =>
        _permisoRepository = permisoRepository ?? throw new ArgumentNullException(nameof(permisoRepository));

    /// <inheritdoc />
    public async Task<PermisosEfectivosDto> ObtenerPermisosAsync(
        int idUsuario,
        CancellationToken cancellationToken = default)
    {
        var roles = await ResolverRolesWebAsync(idUsuario, cancellationToken);

        if (!roles.TieneAccesoWeb)
        {
            return new PermisosEfectivosDto();
        }

        if (roles.AccesoTotal)
        {
            return new PermisosEfectivosDto { TieneAccesoWeb = true, AccesoTotal = true };
        }

        var menus = await _permisoRepository.ObtenerMenusConPermisoAsync(
            roles.IdsRoles,
            AccionPermiso.Ver,
            cancellationToken);

        return new PermisosEfectivosDto
        {
            TieneAccesoWeb = true,
            MenusVisibles = menus.Where(idMenu => idMenu > 0).ToHashSet()
        };
    }

    /// <inheritdoc />
    public async Task<bool> TieneAccesoWebAsync(int idUsuario, CancellationToken cancellationToken = default)
    {
        var permisos = await ObtenerPermisosAsync(idUsuario, cancellationToken);

        return permisos.TieneAccesoWeb;
    }

    /// <inheritdoc />
    public async Task<bool> TienePermisoAsync(
        int idUsuario,
        string menuUrl,
        AccionPermiso accion,
        CancellationToken cancellationToken = default)
    {
        var url = PermisoMenu.NormalizarUrl(menuUrl);

        if (url is null)
        {
            return false;
        }

        var roles = await ResolverRolesWebAsync(idUsuario, cancellationToken);

        if (roles.AccesoTotal)
        {
            return true;
        }

        if (!roles.TieneAccesoWeb)
        {
            return false;
        }

        return await _permisoRepository.TienePermisoAsync(roles.IdsRoles, url, accion, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> PuedeVerAsync(int idUsuario, string menuUrl, CancellationToken cancellationToken = default) =>
        TienePermisoAsync(idUsuario, menuUrl, AccionPermiso.Ver, cancellationToken);

    /// <summary>
    /// Determina los roles que participan de la seguridad web del usuario.
    /// </summary>
    /// <param name="idUsuario">Identificador del usuario autenticado.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Roles web del usuario, si tiene acceso total y si puede usar la aplicación.</returns>
    private async Task<RolesWeb> ResolverRolesWebAsync(int idUsuario, CancellationToken cancellationToken)
    {
        if (idUsuario <= 0)
        {
            return RolesWeb.SinAcceso;
        }

        var asignados = await _permisoRepository.ObtenerRolesActivosAsync(idUsuario, cancellationToken);

        var web = asignados
            .Where(rol => rol.IdRol > 0 && !RolesSistema.EsResponsableMigrador(rol.NombreRol))
            .ToList();

        if (web.Count == 0)
        {
            return RolesWeb.SinAcceso;
        }

        if (web.Exists(rol => RolesSistema.EsAdministrador(rol.NombreRol)))
        {
            return RolesWeb.ConAccesoTotal;
        }

        var idsRoles = web
            .Select(rol => rol.IdRol)
            .Distinct()
            .OrderBy(idRol => idRol)
            .ToList();

        return new RolesWeb(true, false, idsRoles);
    }

    /// <summary>Roles web del usuario y su nivel de acceso.</summary>
    /// <param name="TieneAccesoWeb">Indica si el usuario puede usar la aplicación web.</param>
    /// <param name="AccesoTotal">Indica si el usuario es administrador (acceso total implícito).</param>
    /// <param name="IdsRoles">Roles que otorgan permisos; vacío cuando el acceso es total.</param>
    private sealed record RolesWeb(bool TieneAccesoWeb, bool AccesoTotal, IReadOnlyList<int> IdsRoles)
    {
        /// <summary>Usuario sin ningún rol web: no puede acceder al visor.</summary>
        public static readonly RolesWeb SinAcceso = new(false, false, []);

        /// <summary>Usuario administrador: acceso total sin consultar <c>dbo.RolMenu</c>.</summary>
        public static readonly RolesWeb ConAccesoTotal = new(true, true, []);
    }
}
