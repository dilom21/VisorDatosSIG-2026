using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Security;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Security;

namespace VisorDatosSIG.UnitTests;

/// <summary>
/// Pruebas del resolutor de permisos efectivos con un repositorio simulado, sin SQL Server.
/// </summary>
/// <remarks>
/// Reglas verificadas: el rol del migrador se ignora por completo, el rol <c>Administrador</c> tiene
/// acceso total implícito sin consultar <c>dbo.RolMenu</c> y los demás roles se unen de forma
/// acumulativa.
/// </remarks>
public sealed class PermisoServiceTests
{
    private sealed class PermisoRepositoryFalso : IPermisoRepository
    {
        public List<RolAsignadoDto> Roles { get; } = [];

        public HashSet<int> Menus { get; } = [];

        public bool OtorgaAccion { get; set; }

        public List<int> RolesConsultados { get; } = [];

        public Task<IReadOnlyList<RolAsignadoDto>> ObtenerRolesActivosAsync(
            int idUsuario,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RolAsignadoDto>>(Roles);

        public Task<bool> TienePermisoAsync(
            IReadOnlyList<int> idsRoles,
            string menuUrl,
            AccionPermiso accion,
            CancellationToken cancellationToken = default)
        {
            RolesConsultados.AddRange(idsRoles);

            return Task.FromResult(OtorgaAccion);
        }

        public Task<IReadOnlyList<int>> ObtenerMenusConPermisoAsync(
            IReadOnlyList<int> idsRoles,
            AccionPermiso accion,
            CancellationToken cancellationToken = default)
        {
            RolesConsultados.AddRange(idsRoles);

            return Task.FromResult<IReadOnlyList<int>>([.. Menus.OrderBy(idMenu => idMenu)]);
        }
    }

    private static RolAsignadoDto Rol(int idRol, string nombre) => new() { IdRol = idRol, NombreRol = nombre };

    [Fact(DisplayName = "Permisos: el Administrador tiene acceso total sin consultar la matriz")]
    public async Task ElAdministradorTieneAccesoTotal()
    {
        var repositorio = new PermisoRepositoryFalso { OtorgaAccion = false };
        repositorio.Roles.Add(Rol(1, "Administrador"));

        var servicio = new PermisoService(repositorio);
        var permisos = await servicio.ObtenerPermisosAsync(10);

        Assert.True(permisos.TieneAccesoWeb);
        Assert.True(permisos.AccesoTotal);
        Assert.Empty(permisos.MenusVisibles);
        Assert.True(await servicio.TienePermisoAsync(10, PermisoMenu.Roles, AccionPermiso.Crear));
        Assert.Empty(repositorio.RolesConsultados);
    }

    [Fact(DisplayName = "Permisos: un usuario con solo el rol del migrador no accede al visor web")]
    public async Task ElMigradorNoAccedeAlVisor()
    {
        var repositorio = new PermisoRepositoryFalso();
        repositorio.Roles.Add(Rol(2, "RESPONSABLE MIGRADOR"));

        var servicio = new PermisoService(repositorio);
        var permisos = await servicio.ObtenerPermisosAsync(10);

        Assert.False(permisos.TieneAccesoWeb);
        Assert.False(permisos.AccesoTotal);
        Assert.Empty(permisos.MenusVisibles);
        Assert.False(await servicio.PuedeVerAsync(10, PermisoMenu.Bitacora));
        Assert.False(await servicio.TieneAccesoWebAsync(10));
    }

    [Fact(DisplayName = "Permisos: los roles web se unen y el migrador se descarta de la unión")]
    public async Task LosRolesWebSeUnen()
    {
        var repositorio = new PermisoRepositoryFalso();
        repositorio.Roles.AddRange(
        [
            Rol(7, "MIGRADOR"),
            Rol(4, "Consultor"),
            Rol(9, "Operador")
        ]);
        repositorio.Menus.Add(3);
        repositorio.Menus.Add(6);

        var permisos = await new PermisoService(repositorio).ObtenerPermisosAsync(10);

        Assert.True(permisos.TieneAccesoWeb);
        Assert.False(permisos.AccesoTotal);
        Assert.Equal(new[] { 3, 6 }, permisos.MenusVisibles.OrderBy(idMenu => idMenu));
        Assert.Equal(new[] { 4, 9 }, repositorio.RolesConsultados.Distinct().OrderBy(idRol => idRol));
    }

    [Fact(DisplayName = "Permisos: un usuario sin roles activos no tiene acceso ni permisos")]
    public async Task SinRolesNoHayAcceso()
    {
        var repositorio = new PermisoRepositoryFalso { OtorgaAccion = true };
        var servicio = new PermisoService(repositorio);

        Assert.False((await servicio.ObtenerPermisosAsync(10)).TieneAccesoWeb);
        Assert.False(await servicio.TienePermisoAsync(10, PermisoMenu.Roles, AccionPermiso.Ver));
        Assert.False((await servicio.ObtenerPermisosAsync(0)).TieneAccesoWeb);
    }

    [Fact(DisplayName = "Permisos: una ruta vacía nunca autoriza")]
    public async Task LaRutaVaciaNoAutoriza()
    {
        var repositorio = new PermisoRepositoryFalso { OtorgaAccion = true };
        repositorio.Roles.Add(Rol(4, "Consultor"));

        Assert.False(await new PermisoService(repositorio).TienePermisoAsync(10, "   ", AccionPermiso.Ver));
    }
}
