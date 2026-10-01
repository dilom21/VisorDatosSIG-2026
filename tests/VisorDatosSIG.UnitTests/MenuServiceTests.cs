using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Navigation;
using VisorDatosSIG.Application.DTOs.Security;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Navigation;

namespace VisorDatosSIG.UnitTests;

/// <summary>
/// Pruebas de la construcción de la jerarquía del menú lateral dinámico
/// (<c>GET /api/menu</c>) con un repositorio simulado, sin SQL Server.
/// </summary>
/// <remarks>
/// La jerarquía se resuelve por identificador (<c>IdMenu</c> / <c>IdMenuPadre</c>), nunca
/// por nombre, y el orden se define con <c>Orden</c> y, ante empate, con <c>IdMenu</c>.
/// </remarks>
public sealed class MenuServiceTests
{
    private sealed class MenuRepositoryFalso(IReadOnlyList<MenuOpcionPlanaDto> filas) : IMenuRepository
    {
        public CancellationToken TokenRecibido { get; private set; }

        /// <summary>Indica si el servicio llegó a consultar las opciones de menú.</summary>
        public bool Consultado { get; private set; }

        public Task<IReadOnlyList<MenuOpcionPlanaDto>> ObtenerOpcionesAsync(
            CancellationToken cancellationToken = default)
        {
            TokenRecibido = cancellationToken;
            Consultado = true;
            return Task.FromResult(filas);
        }
    }

    private sealed class PermisoServiceFalso(PermisosEfectivosDto permisos) : IPermisoService
    {
        public CancellationToken TokenRecibido { get; private set; }

        public Task<PermisosEfectivosDto> ObtenerPermisosAsync(
            int idUsuario,
            CancellationToken cancellationToken = default)
        {
            TokenRecibido = cancellationToken;
            return Task.FromResult(permisos);
        }

        public Task<bool> TieneAccesoWebAsync(int idUsuario, CancellationToken cancellationToken = default) =>
            Task.FromResult(permisos.TieneAccesoWeb);

        public Task<bool> TienePermisoAsync(
            int idUsuario,
            string menuUrl,
            AccionPermiso accion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(permisos.AccesoTotal);

        public Task<bool> PuedeVerAsync(int idUsuario, string menuUrl, CancellationToken cancellationToken = default) =>
            Task.FromResult(permisos.AccesoTotal);

        public static PermisosEfectivosDto AccesoTotal() => new() { TieneAccesoWeb = true, AccesoTotal = true };

        public static PermisosEfectivosDto ConMenus(params int[] idsMenu) => new()
        {
            TieneAccesoWeb = true,
            MenusVisibles = idsMenu.ToHashSet()
        };

        public static PermisosEfectivosDto SinAccesoWeb() => new();
    }

    private static MenuOpcionPlanaDto Fila(
        int idMenu,
        int? idMenuPadre,
        string nombre,
        string? url = null,
        string? icono = null,
        int orden = 0,
        int estado = 1) => new()
    {
        IdMenu = idMenu,
        IdMenuPadre = idMenuPadre,
        NombreMenu = nombre,
        Url = url,
        Icono = icono,
        Orden = orden,
        Estado = estado
    };

    [Fact(DisplayName = "Menú: anida las hijas por identificador y ordena niveles por Orden e IdMenu")]
    public void AnidaPorIdentificadorYOrdena()
    {
        // Las filas llegan desordenadas y con nombres que no coinciden con la jerarquía:
        // la relación padre/hijo debe resolverse solo por identificador.
        var filas = new[]
        {
            Fila(5, 2, "Vías", "/Consultas/Vias", orden: 2),
            Fila(1, null, "Usuarios y seguridad", icono: "shield", orden: 1),
            Fila(4, 2, "Lotes", "/Consultas/Lotes", orden: 1),
            Fila(2, null, "Consultas y filtros", icono: "search", orden: 2),
            Fila(3, 1, "Usuarios", "/Usuarios", icono: "users", orden: 1)
        };

        var menu = MenuService.ConstruirJerarquia(filas);

        Assert.Equal(2, menu.Count);
        Assert.Equal(new[] { "Usuarios y seguridad", "Consultas y filtros" }, menu.Select(o => o.NombreMenu));

        var seguridad = menu[0];
        Assert.Equal(1, seguridad.IdMenu);
        Assert.Null(seguridad.Url);

        var hijo = Assert.Single(seguridad.Opciones);
        Assert.Equal("Usuarios", hijo.NombreMenu);
        Assert.Equal("/Usuarios", hijo.Url);
        Assert.Equal("users", hijo.Icono);

        var consultas = menu[1];
        Assert.Equal(new[] { "Lotes", "Vías" }, consultas.Opciones.Select(o => o.NombreMenu));
        Assert.Equal(new[] { "/Consultas/Lotes", "/Consultas/Vias" }, consultas.Opciones.Select(o => o.Url));
    }

    [Fact(DisplayName = "Menú: ante el mismo Orden el desempate es por IdMenu")]
    public void DesempataPorIdentificador()
    {
        var filas = new[]
        {
            Fila(9, null, "Novena", orden: 0),
            Fila(3, null, "Tercera", orden: 0),
            Fila(7, 3, "Hija B", orden: 0),
            Fila(2, 3, "Hija A", orden: 0)
        };

        var menu = MenuService.ConstruirJerarquia(filas);

        Assert.Equal(new[] { 3, 9 }, menu.Select(o => o.IdMenu));
        Assert.Equal(new[] { 2, 7 }, menu[0].Opciones.Select(o => o.IdMenu));
    }

    [Fact(DisplayName = "Menú: las opciones inactivas no se incluyen")]
    public void OmiteOpcionesInactivas()
    {
        var filas = new[]
        {
            Fila(1, null, "Usuarios y seguridad", orden: 1),
            Fila(2, null, "Migrador de datos geográficos", orden: 2, estado: 0),
            Fila(3, 1, "Roles", "/Roles", orden: 2)
        };

        var menu = MenuService.ConstruirJerarquia(filas);

        var unico = Assert.Single(menu);
        Assert.Equal("Usuarios y seguridad", unico.NombreMenu);
        Assert.Equal(new[] { "Roles" }, unico.Opciones.Select(o => o.NombreMenu));
    }

    [Fact(DisplayName = "Menú: la hija de un padre inactivo no se promueve a primer nivel")]
    public void DescartaHijasHuerfanas()
    {
        var filas = new[]
        {
            Fila(1, null, "Usuarios y seguridad", orden: 1, estado: 0),
            Fila(2, 1, "Roles", "/Roles", orden: 1),
            Fila(3, null, "Reportes", "/Reportes", orden: 2)
        };

        var menu = MenuService.ConstruirJerarquia(filas);

        var unico = Assert.Single(menu);
        Assert.Equal("Reportes", unico.NombreMenu);
        Assert.Empty(unico.Opciones);
    }

    [Fact(DisplayName = "Menú: Url e Icono vacíos se devuelven como null y sin espacios sobrantes")]
    public void NormalizaUrlEIcono()
    {
        var filas = new[]
        {
            Fila(1, null, "Consultas y filtros", url: "   ", icono: string.Empty, orden: 1),
            Fila(2, null, "Reportes", url: "  /Reportes  ", icono: "  chart ", orden: 2)
        };

        var menu = MenuService.ConstruirJerarquia(filas);

        Assert.Null(menu[0].Url);
        Assert.Null(menu[0].Icono);
        Assert.Equal("/Reportes", menu[1].Url);
        Assert.Equal("chart", menu[1].Icono);
    }

    [Fact(DisplayName = "Menú: sin filas válidas devuelve una colección vacía")]
    public void SinFilasValidasDevuelveVacio()
    {
        Assert.Empty(MenuService.ConstruirJerarquia([]));
        Assert.Empty(MenuService.ConstruirJerarquia(null));
        Assert.Empty(MenuService.ConstruirJerarquia(
            [Fila(0, null, "Identificador inválido"), Fila(2, null, "Inactiva", estado: 0)]));
    }

    [Fact(DisplayName = "Menú: una referencia circular no provoca recursión infinita")]
    public void ReferenciaCircularNoSeDesborda()
    {
        var filas = new[]
        {
            Fila(1, null, "Inicio"),
            Fila(2, 1, "Nivel 2"),
            Fila(3, 2, "Nivel 3"),
            // Apunta de vuelta a una opción ya visitada.
            Fila(1, 3, "Inicio (circular)")
        };

        var menu = MenuService.ConstruirJerarquia(filas);

        var nivel1 = Assert.Single(menu);
        var nivel2 = Assert.Single(nivel1.Opciones);
        var nivel3 = Assert.Single(nivel2.Opciones);
        // El ciclo se corta: la tercera hija no vuelve a incluir lo ya visitado.
        Assert.Empty(nivel3.Opciones);
    }

    [Fact(DisplayName = "Menú: el servicio delega en el repositorio y propaga el CancellationToken")]
    public async Task ElServicioDelegaEnElRepositorio()
    {
        var repositorio = new MenuRepositoryFalso([Fila(1, null, "Reportes", "/Reportes")]);
        var permisos = new PermisoServiceFalso(PermisoServiceFalso.AccesoTotal());
        var servicio = new MenuService(repositorio, permisos);

        using var cancelacion = new CancellationTokenSource();
        var menu = await servicio.ObtenerMenuAsync(7, cancelacion.Token);

        Assert.Equal(cancelacion.Token, repositorio.TokenRecibido);
        Assert.Equal(cancelacion.Token, permisos.TokenRecibido);
        Assert.True(menu.TieneAccesoWeb);
        Assert.Equal(new[] { "Reportes" }, menu.Opciones.Select(o => o.NombreMenu));
    }

    [Fact(DisplayName = "Menú: el administrador (acceso total) recibe todas las opciones activas")]
    public async Task ElAdministradorRecibeTodasLasOpciones()
    {
        var filas = new[]
        {
            Fila(1, null, "Usuarios y seguridad", orden: 1),
            Fila(2, 1, "Roles", "/Roles", orden: 1),
            Fila(3, null, "Reportes", "/Reportes", orden: 2)
        };

        var servicio = new MenuService(
            new MenuRepositoryFalso(filas),
            new PermisoServiceFalso(PermisoServiceFalso.AccesoTotal()));

        var menu = await servicio.ObtenerMenuAsync(1, CancellationToken.None);

        Assert.Equal(new[] { "Usuarios y seguridad", "Reportes" }, menu.Opciones.Select(o => o.NombreMenu));
        Assert.Single(menu.Opciones[0].Opciones);
    }

    [Fact(DisplayName = "Menú: solo se muestran las opciones con permiso de ver y los agrupadores con hijas visibles")]
    public async Task FiltraPorPermisosDeVer()
    {
        var filas = new[]
        {
            Fila(1, null, "Usuarios y seguridad", orden: 1),
            Fila(2, 1, "Usuarios", "/Usuarios", orden: 1),
            Fila(3, 1, "Roles", "/Roles", orden: 2),
            Fila(4, null, "Consultas y filtros", orden: 2),
            Fila(5, 4, "Lotes", "/Consultas/Lotes", orden: 1),
            Fila(6, null, "Reportes", "/Reportes", orden: 3)
        };

        // El usuario solo puede ver "Roles": el agrupador de consultas queda fuera por no tener
        // hijas visibles y "Usuarios" no aparece aunque comparta padre.
        var servicio = new MenuService(
            new MenuRepositoryFalso(filas),
            new PermisoServiceFalso(PermisoServiceFalso.ConMenus(3)));

        var menu = await servicio.ObtenerMenuAsync(5, CancellationToken.None);

        var seguridad = Assert.Single(menu.Opciones);
        Assert.Equal("Usuarios y seguridad", seguridad.NombreMenu);
        var unico = Assert.Single(seguridad.Opciones);
        Assert.Equal("/Roles", unico.Url);
    }

    [Fact(DisplayName = "Menú: un usuario sin roles web no recibe opciones y no se consulta el menú")]
    public async Task SinAccesoWebNoDevuelveOpciones()
    {
        var repositorio = new MenuRepositoryFalso([Fila(1, null, "Reportes", "/Reportes")]);
        var servicio = new MenuService(repositorio, new PermisoServiceFalso(PermisoServiceFalso.SinAccesoWeb()));

        var menu = await servicio.ObtenerMenuAsync(9, CancellationToken.None);

        Assert.False(menu.TieneAccesoWeb);
        Assert.Empty(menu.Opciones);
        Assert.False(repositorio.Consultado);
    }
}
