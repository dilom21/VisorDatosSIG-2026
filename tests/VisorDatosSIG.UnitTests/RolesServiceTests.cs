using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Application.DTOs.Navigation;
using VisorDatosSIG.Application.DTOs.Security;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Security;

namespace VisorDatosSIG.UnitTests;

/// <summary>
/// Pruebas de las reglas de negocio de la administración de roles y permisos (CU03 y CU04) con
/// repositorios simulados, sin SQL Server.
/// </summary>
/// <remarks>
/// Se verifican las reglas que el controlador no debe conocer: exclusión del rol del migrador,
/// protección de <c>Administrador</c>, nombres reservados y duplicados, roles en uso, baja lógica y
/// reemplazo completo de la matriz de permisos junto con su evento de auditoría.
/// </remarks>
public sealed class RolesServiceTests
{
    private const string RolMigrador = "RESPONSABLE MIGRADOR";

    private sealed class RolRepositoryFalso(params RolFilaDto[] roles) : IRolRepository
    {
        private readonly List<RolFilaDto> _roles = [.. roles];

        public List<BitacoraRegistroDto> Auditorias { get; } = [];

        public List<BitacoraRegistroDto> AuditoriasPermisos { get; } = [];

        public int IdCreado { get; set; } = 42;

        public bool NombreDuplicado { get; set; }

        public bool MenusValidos { get; set; } = true;

        public bool EstadoActualizado { get; set; } = true;

        public IReadOnlyList<PermisoMenuFilaDto> PermisosVigentes { get; private set; } = [];

        public IReadOnlyList<PermisoMenuFilaDto> PermisosGuardados { get; private set; } = [];

        /// <summary>Fija los permisos que el simulador devolverá como vigentes.</summary>
        public void EstablecerPermisos(params PermisoMenuFilaDto[] permisos) => PermisosVigentes = permisos;

        public Task<IReadOnlyList<RolFilaDto>> ObtenerRolesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RolFilaDto>>(_roles);

        public Task<RolFilaDto?> ObtenerRolPorIdAsync(int idRol, CancellationToken cancellationToken = default) =>
            Task.FromResult(_roles.FirstOrDefault(rol => rol.IdRol == idRol));

        public Task<bool> ExisteNombreRolAsync(
            string nombreRol,
            int? idRolExcluido,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(NombreDuplicado);

        public Task<int> CrearRolAsync(
            string nombreRol,
            string? descripcion,
            Func<int, BitacoraRegistroDto> crearAuditoria,
            CancellationToken cancellationToken = default)
        {
            Auditorias.Add(crearAuditoria(IdCreado));
            return Task.FromResult(IdCreado);
        }

        public Task<bool> ActualizarRolAsync(
            int idRol,
            string nombreRol,
            string? descripcion,
            BitacoraRegistroDto auditoria,
            CancellationToken cancellationToken = default)
        {
            Auditorias.Add(auditoria);
            return Task.FromResult(_roles.Any(rol => rol.IdRol == idRol));
        }

        public Task<bool> ActualizarEstadoRolAsync(
            int idRol,
            bool activo,
            BitacoraRegistroDto auditoria,
            CancellationToken cancellationToken = default)
        {
            Auditorias.Add(auditoria);
            return Task.FromResult(EstadoActualizado && _roles.Any(rol => rol.IdRol == idRol));
        }

        public Task<IReadOnlyList<PermisoMenuFilaDto>> ObtenerPermisosRolAsync(
            int idRol,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PermisosVigentes);

        public Task<bool> ExistenMenusActivosAsync(
            IReadOnlyList<int> idsMenu,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(MenusValidos);

        public Task<bool> GuardarPermisosRolAsync(
            int idRol,
            IReadOnlyList<PermisoMenuFilaDto> permisos,
            BitacoraRegistroDto auditoria,
            CancellationToken cancellationToken = default)
        {
            // El simulador persiste lo enviado: así la matriz devuelta tras guardar es verificable.
            PermisosGuardados = permisos;
            PermisosVigentes = permisos;
            AuditoriasPermisos.Add(auditoria);

            return Task.FromResult(_roles.Any(rol => rol.IdRol == idRol));
        }
    }

    private sealed class MenuRepositoryFalso(IReadOnlyList<MenuOpcionPlanaDto> filas) : IMenuRepository
    {
        public Task<IReadOnlyList<MenuOpcionPlanaDto>> ObtenerOpcionesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(filas);
    }

    private static MenuOpcionPlanaDto Menu(
        int idMenu,
        int? idMenuPadre,
        string nombre,
        string? url = null,
        int orden = 0) => new()
    {
        IdMenu = idMenu,
        IdMenuPadre = idMenuPadre,
        NombreMenu = nombre,
        Url = url,
        Orden = orden,
        Estado = 1
    };

    private static RolFilaDto Rol(
        int idRol,
        string nombreRol,
        bool activo = true,
        int usuarios = 0,
        string? descripcion = null) => new()
    {
        IdRol = idRol,
        NombreRol = nombreRol,
        Descripcion = descripcion,
        Activo = activo,
        CantidadUsuariosActivos = usuarios
    };

    /// <summary>Menú mínimo de las pruebas: dos agrupadores con opciones navegables.</summary>
    private static MenuOpcionPlanaDto[] MenusDeSistema() =>
    [
        Menu(1, null, "Usuarios y seguridad", orden: 1),
        Menu(2, 1, "Roles", "/Roles", orden: 1),
        Menu(3, 1, "Bitácora", "/Bitacora", orden: 2),
        Menu(4, null, "Visor cartográfico", orden: 2),
        Menu(5, 4, "Visor", "/Visor", orden: 1)
    ];

    private static RolesService Servicio(RolRepositoryFalso repositorio, params MenuOpcionPlanaDto[] menus) =>
        new(repositorio, new MenuRepositoryFalso(menus.Length == 0 ? MenusDeSistema() : menus));

    private static ContextoAuditoriaDto AuditoriaDe() => new() { IdUsuario = 7, Ip = "10.0.0.5" };

    private static RolSolicitudDto Solicitud(string nombre, string? descripcion = null) =>
        new() { NombreRol = nombre, Descripcion = descripcion };

    [Fact(DisplayName = "Roles: el listado excluye el rol del migrador y marca el rol del sistema")]
    public async Task ElListadoExcluyeElMigrador()
    {
        var repositorio = new RolRepositoryFalso(
            Rol(1, "Administrador"),
            Rol(2, RolMigrador),
            Rol(3, "Consultor", usuarios: 2));

        var roles = await Servicio(repositorio).ObtenerRolesAsync();

        Assert.Equal(new[] { "Administrador", "Consultor" }, roles.Select(rol => rol.NombreRol));
        Assert.True(roles[0].EsRolSistema);
        Assert.False(roles[1].EsRolSistema);
        Assert.Equal(2, roles[1].CantidadUsuariosActivos);
    }

    [Fact(DisplayName = "Roles: el rol del migrador no se puede consultar por identificador")]
    public async Task ElMigradorNoSeConsulta()
    {
        var repositorio = new RolRepositoryFalso(Rol(2, RolMigrador));

        Assert.Null(await Servicio(repositorio).ObtenerRolAsync(2));
    }

    [Theory(DisplayName = "Roles: los nombres reservados se rechazan al crear")]
    [InlineData("Administrador")]
    [InlineData("administrador general")]
    [InlineData("responsable migrador")]
    [InlineData("MIGRADOR")]
    public async Task LosNombresReservadosSeRechazan(string nombre)
    {
        var resultado = await Servicio(new RolRepositoryFalso()).CrearRolAsync(Solicitud(nombre), AuditoriaDe());

        Assert.Equal(ResultadoRol.NombreReservado, resultado.Estado);
        Assert.Null(resultado.Rol);
    }

    [Fact(DisplayName = "Roles: un nombre duplicado no se crea")]
    public async Task ElNombreDuplicadoNoSeCrea()
    {
        var repositorio = new RolRepositoryFalso { NombreDuplicado = true };

        var resultado = await Servicio(repositorio).CrearRolAsync(Solicitud("Consultor"), AuditoriaDe());

        Assert.Equal(ResultadoRol.NombreDuplicado, resultado.Estado);
    }

    [Fact(DisplayName = "Roles: un nombre vacío o demasiado largo es inválido")]
    public async Task ElNombreInvalidoSeRechaza()
    {
        var servicio = Servicio(new RolRepositoryFalso());

        var vacio = await servicio.CrearRolAsync(Solicitud("   "), AuditoriaDe());
        var largo = await servicio.CrearRolAsync(Solicitud(new string('A', 51)), AuditoriaDe());

        Assert.Equal(ResultadoRol.DatosInvalidos, vacio.Estado);
        Assert.Equal(ResultadoRol.DatosInvalidos, largo.Estado);
    }

    [Fact(DisplayName = "Roles: crear un rol registra la auditoría con el identificador generado")]
    public async Task CrearRolRegistraAuditoria()
    {
        var repositorio = new RolRepositoryFalso { IdCreado = 77 };

        var resultado = await Servicio(repositorio)
            .CrearRolAsync(Solicitud("  Consultor  ", "  Solo lectura "), AuditoriaDe());

        Assert.True(resultado.IsSuccess);
        Assert.Equal(77, resultado.Rol!.IdRol);
        Assert.Equal("Consultor", resultado.Rol.NombreRol);
        Assert.Equal("Solo lectura", resultado.Rol.Descripcion);
        Assert.True(resultado.Rol.Activo);

        var auditoria = Assert.Single(repositorio.Auditorias);
        Assert.Equal("Crear Rol", auditoria.Accion);
        Assert.Equal("Roles", auditoria.Entidad);
        Assert.Equal(77, auditoria.IdEntidad);
        Assert.Equal(7, auditoria.IdUsuario);
        Assert.Equal("10.0.0.5", auditoria.Ip);
    }

    [Fact(DisplayName = "Roles: el rol Administrador no se modifica ni se deshabilita")]
    public async Task ElAdministradorEstaProtegido()
    {
        var repositorio = new RolRepositoryFalso(Rol(1, "Administrador"));
        var servicio = Servicio(repositorio);

        var modificado = await servicio.ActualizarRolAsync(1, Solicitud("Administrador del sistema"), AuditoriaDe());
        var deshabilitado = await servicio.CambiarEstadoAsync(1, false, AuditoriaDe());

        Assert.Equal(ResultadoRol.RolProtegido, modificado.Estado);
        Assert.Equal(ResultadoRol.RolProtegido, deshabilitado.Estado);
        Assert.Empty(repositorio.Auditorias);
    }

    [Fact(DisplayName = "Roles: un rol con usuarios activos no se deshabilita")]
    public async Task ElRolEnUsoNoSeDeshabilita()
    {
        var repositorio = new RolRepositoryFalso(Rol(5, "Consultor", usuarios: 3));

        var resultado = await Servicio(repositorio).CambiarEstadoAsync(5, false, AuditoriaDe());

        Assert.Equal(ResultadoRol.RolEnUso, resultado.Estado);
        Assert.Empty(repositorio.Auditorias);
    }

    [Fact(DisplayName = "Roles: deshabilitar un rol sin usuarios cambia el estado y audita")]
    public async Task ElRolSinUsuariosSeDeshabilita()
    {
        var repositorio = new RolRepositoryFalso(Rol(5, "Consultor"));

        var resultado = await Servicio(repositorio).CambiarEstadoAsync(5, false, AuditoriaDe());

        Assert.True(resultado.IsSuccess);
        Assert.False(resultado.Rol!.Activo);

        var auditoria = Assert.Single(repositorio.Auditorias);
        Assert.Equal("Cambiar Estado Rol", auditoria.Accion);
        Assert.Equal(5, auditoria.IdEntidad);
    }

    [Fact(DisplayName = "Roles: repetir el estado vigente es idempotente y no audita")]
    public async Task ElEstadoRepetidoNoAudita()
    {
        var repositorio = new RolRepositoryFalso(Rol(5, "Consultor", activo: false));

        var resultado = await Servicio(repositorio).CambiarEstadoAsync(5, false, AuditoriaDe());

        Assert.True(resultado.IsSuccess);
        Assert.False(resultado.Rol!.Activo);
        Assert.Empty(repositorio.Auditorias);
    }

    [Fact(DisplayName = "Roles: el catálogo de permisos respeta la jerarquía y el orden del menú")]
    public async Task ElCatalogoRespetaLaJerarquia()
    {
        var catalogo = await Servicio(new RolRepositoryFalso()).ObtenerCatalogoPermisosAsync();

        Assert.Equal(
            new[] { "Usuarios y seguridad", "Visor cartográfico" },
            catalogo.Select(opcion => opcion.NombreMenu));

        var seguridad = catalogo[0];
        Assert.Null(seguridad.Url);
        Assert.Equal(new[] { "Roles", "Bitácora" }, seguridad.Opciones.Select(opcion => opcion.NombreMenu));
        Assert.Equal("/Roles", seguridad.Opciones[0].Url);
    }

    [Fact(DisplayName = "Roles: la matriz devuelve una fila por opción activa con los permisos vigentes")]
    public async Task LaMatrizDevuelveTodasLasOpciones()
    {
        var repositorio = new RolRepositoryFalso(Rol(5, "Consultor"));
        repositorio.EstablecerPermisos(
            new PermisoMenuFilaDto { IdMenu = 2, PuedeVer = true, PuedeEditar = true });

        var permisos = await Servicio(repositorio).ObtenerPermisosAsync(5);

        Assert.NotNull(permisos);
        Assert.False(permisos!.EsRolSistema);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, permisos.Menus.Select(menu => menu.IdMenu));

        var roles = permisos.Menus[1];
        Assert.Equal(1, roles.IdMenuPadre);
        Assert.True(roles.PuedeVer);
        Assert.True(roles.PuedeEditar);
        Assert.False(roles.PuedeCrear);
        Assert.False(roles.EsAgrupador);

        var agrupador = permisos.Menus[0];
        Assert.True(agrupador.EsAgrupador);
        Assert.False(agrupador.PuedeVer);
    }

    [Fact(DisplayName = "Roles: la matriz del rol Administrador muestra acceso total implícito")]
    public async Task LaMatrizDelAdministradorMuestraAccesoTotal()
    {
        var repositorio = new RolRepositoryFalso(Rol(1, "Administrador"));

        var permisos = await Servicio(repositorio).ObtenerPermisosAsync(1);

        Assert.NotNull(permisos);
        Assert.True(permisos!.EsRolSistema);
        Assert.All(permisos.Menus, menu =>
        {
            Assert.True(menu.PuedeVer);
            Assert.True(menu.PuedeCrear);
            Assert.True(menu.PuedeEditar);
            Assert.True(menu.PuedeEliminar);
        });
    }

    [Fact(DisplayName = "Roles: guardar la matriz reemplaza los permisos y audita el cambio")]
    public async Task GuardarLaMatrizReemplazaLosPermisos()
    {
        var repositorio = new RolRepositoryFalso(Rol(5, "Consultor"));

        var resultado = await Servicio(repositorio).GuardarPermisosAsync(
            5,
            new RolPermisosSolicitudDto
            {
                Permisos =
                [
                    new PermisoMenuSolicitudDto { IdMenu = 2, PuedeVer = true },
                    new PermisoMenuSolicitudDto { IdMenu = 3 },
                    new PermisoMenuSolicitudDto { IdMenu = 5, PuedeVer = true, PuedeCrear = true }
                ]
            },
            AuditoriaDe());

        Assert.True(resultado.IsSuccess);

        // La fila sin permisos (IdMenu 3) no se persiste: su ausencia significa lo mismo.
        Assert.Equal(new[] { 2, 5 }, repositorio.PermisosGuardados.Select(permiso => permiso.IdMenu));

        var auditoria = Assert.Single(repositorio.AuditoriasPermisos);
        Assert.Equal("Actualizar Permisos Rol", auditoria.Accion);
        Assert.Equal("RolMenu", auditoria.Entidad);
        Assert.Equal(5, auditoria.IdEntidad);

        var roles = Assert.Single(resultado.Permisos!.Menus, menu => menu.IdMenu == 2);
        Assert.True(roles.PuedeVer);
        Assert.False(roles.PuedeEliminar);
    }

    [Fact(DisplayName = "Roles: la matriz con menús inexistentes o inactivos se rechaza")]
    public async Task LaMatrizConMenusInvalidosSeRechaza()
    {
        var repositorio = new RolRepositoryFalso(Rol(5, "Consultor")) { MenusValidos = false };

        var resultado = await Servicio(repositorio).GuardarPermisosAsync(
            5,
            new RolPermisosSolicitudDto
            {
                Permisos = [new PermisoMenuSolicitudDto { IdMenu = 99, PuedeVer = true }]
            },
            AuditoriaDe());

        Assert.Equal(ResultadoRol.DatosInvalidos, resultado.Estado);
        Assert.Empty(repositorio.PermisosGuardados);
    }

    [Fact(DisplayName = "Roles: el Administrador no admite cambios en su matriz de permisos")]
    public async Task ElAdministradorNoAdmitePermisos()
    {
        var repositorio = new RolRepositoryFalso(Rol(1, "Administrador"));

        var resultado = await Servicio(repositorio).GuardarPermisosAsync(
            1,
            new RolPermisosSolicitudDto
            {
                Permisos = [new PermisoMenuSolicitudDto { IdMenu = 2, PuedeVer = true }]
            },
            AuditoriaDe());

        Assert.Equal(ResultadoRol.RolProtegido, resultado.Estado);
        Assert.Empty(repositorio.PermisosGuardados);
    }

    [Fact(DisplayName = "Roles: la matriz de un rol inexistente o del migrador no se consulta")]
    public async Task LaMatrizDelMigradorNoSeConsulta()
    {
        var repositorio = new RolRepositoryFalso(Rol(2, RolMigrador));
        var servicio = Servicio(repositorio);

        Assert.Null(await servicio.ObtenerPermisosAsync(2));
        Assert.Null(await servicio.ObtenerPermisosAsync(404));
    }
}
