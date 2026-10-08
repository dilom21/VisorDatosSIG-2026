using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using VisorDatosSIG.Api.Controllers;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Application.DTOs.Security;
using VisorDatosSIG.Application.DTOs.Servicios;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.UnitTests;

public sealed class ServiciosControllerTests
{
    private sealed class FakeServicioRepository : IServicioRepository
    {
        public List<ServicioDto> Items { get; } =
        [
            Servicio(1, "SIG-001", 1001, "Ana Uno", 1),
            Servicio(2, "SIG-002", 1002, "Bruno Dos", 2),
            Servicio(3, "SIG-003", 1003, "Carla Tres", 3)
        ];

        public string? UltimaBusqueda { get; private set; }
        public byte? UltimoEstado { get; private set; }
        public int UltimaPagina { get; private set; }
        public int UltimoLimite { get; private set; }

        public Task<PagedResult<ServicioDto>> ObtenerTodosAsync(string? busqueda = null, byte? estado = null,
            int pagina = 1, int limite = 20, CancellationToken ct = default)
        {
            UltimaBusqueda = busqueda;
            UltimoEstado = estado;
            UltimaPagina = pagina;
            UltimoLimite = limite;
            var datos = Items.Where(x =>
                (string.IsNullOrWhiteSpace(busqueda) ||
                (x.CodF_SIG?.Contains(busqueda, StringComparison.OrdinalIgnoreCase) ?? false) ||
                 (x.CodFijo?.ToString().Contains(busqueda, StringComparison.OrdinalIgnoreCase) ?? false) ||
                 (x.Nombre?.Contains(busqueda, StringComparison.OrdinalIgnoreCase) ?? false)) &&
                (!estado.HasValue || x.Estado == estado.Value)).ToList();
            return Task.FromResult(new PagedResult<ServicioDto>
            {
                Pagina = pagina,
                Limite = limite,
                TotalRegistros = datos.Count,
                Datos = datos.Skip((pagina - 1) * limite).Take(limite).ToList()
            });
        }

        public Task<ServicioDto?> ObtenerPorIdAsync(int idCodigo, CancellationToken ct = default) =>
            Task.FromResult(Items.SingleOrDefault(x => x.IdCodigo == idCodigo));

        public Task<CambioEstadoServicioDto?> CambiarEstadoAsync(int idCodigo, byte estadoNuevo, CancellationToken ct = default)
        {
            var index = Items.FindIndex(x => x.IdCodigo == idCodigo);
            if (index < 0) return Task.FromResult<CambioEstadoServicioDto?>(null);
            var actual = Items[index];
            var actualizado = actual with
            {
                Estado = estadoNuevo,
                EstadoNombre = ServicioRepositoryEstadoNombre(estadoNuevo),
                FechaCambioEstado = DateTime.UtcNow
            };
            Items[index] = actualizado;
            return Task.FromResult<CambioEstadoServicioDto?>(new(actualizado, actual.Estado, estadoNuevo));
        }

        public Task<ServicioResumenDto> ObtenerResumenAsync(CancellationToken ct = default) =>
            Task.FromResult(new ServicioResumenDto(Items.Count, Items.Count(x => x.Estado == 1),
                Items.Count(x => x.Estado == 2), Items.Count(x => x.Estado == 3),
                Items.Count(x => x.Estado == 4), Items.Count(x => x.Estado == 5)));

        private static string ServicioRepositoryEstadoNombre(byte estado) => estado switch
        {
            1 => "Normal", 2 => "Para Corte", 3 => "Cortado", 4 => "Baja Parcial", 5 => "Baja Total", _ => "Desconocido"
        };

        private static ServicioDto Servicio(int id, string sig, int fijo, string nombre, byte estado) =>
            new(id, id, sig, fijo, nombre, estado, ServicioRepositoryEstadoNombre(estado), DateTime.UtcNow, 10, -68.1, -16.5);
    }

    private sealed class FakeBitacoraRepository : IBitacoraRepository
    {
        public List<BitacoraRegistroDto> Registros { get; } = [];
        public Task RegistrarAsync(BitacoraRegistroDto registro, CancellationToken cancellationToken = default)
        {
            Registros.Add(registro);
            return Task.CompletedTask;
        }
    }

    private sealed class FakePermisoService(bool puedeVer, bool puedeEditar) : IPermisoService
    {
        public List<(string Url, AccionPermiso Accion)> Consultas { get; } = [];
        public Task<PermisosEfectivosDto> ObtenerPermisosAsync(int idUsuario, CancellationToken ct = default) =>
            Task.FromResult(new PermisosEfectivosDto { TieneAccesoWeb = puedeVer });
        public Task<bool> TieneAccesoWebAsync(int idUsuario, CancellationToken ct = default) => Task.FromResult(puedeVer);
        public Task<bool> TienePermisoAsync(int idUsuario, string menuUrl, AccionPermiso accion, CancellationToken ct = default)
        {
            Consultas.Add((menuUrl, accion));
            return Task.FromResult(accion == AccionPermiso.Ver ? puedeVer : puedeEditar);
        }
        public Task<bool> PuedeVerAsync(int idUsuario, string menuUrl, CancellationToken ct = default) =>
            TienePermisoAsync(idUsuario, menuUrl, AccionPermiso.Ver, ct);
    }

    private static (ServiciosController Controller, FakeServicioRepository Repo, FakeBitacoraRepository Bitacora,
        FakePermisoService Permisos) Crear(bool puedeVer = true, bool puedeEditar = true)
    {
        var repo = new FakeServicioRepository();
        var bitacora = new FakeBitacoraRepository();
        var permisos = new FakePermisoService(puedeVer, puedeEditar);
        var controller = new ServiciosController(repo, bitacora, permisos,
            NullLogger<ServiciosController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(JwtRegisteredClaimNames.Sub, "7")], "Bearer"))
                }
            }
        };
        return (controller, repo, bitacora, permisos);
    }

    [Fact]
    public async Task ObtenerTodos_Autorizado_DevuelvePaginaYConsultaServicios()
    {
        var (controller, repo, _, permisos) = Crear();
        var result = await controller.ObtenerTodos(null, null, 2, 1, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var page = Assert.IsType<PagedResult<ServicioDto>>(ok.Value);
        Assert.Equal(2, page.Pagina);
        Assert.Single(page.Datos);
        Assert.Equal(PermisoMenu.Servicios, permisos.Consultas[0].Url);
        Assert.Equal(2, repo.UltimaPagina);
    }

    [Fact]
    public async Task ObtenerTodos_SinPermiso_Devuelve403YNoConsulta()
    {
        var (controller, repo, _, _) = Crear(false);
        var result = await controller.ObtenerTodos(null, null, 1, 20, CancellationToken.None);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Null(repo.UltimaBusqueda);
    }

    [Fact]
    public async Task ObtenerTodos_AplicaBusquedaYFiltroEstado()
    {
        var (controller, repo, _, _) = Crear();
        await controller.ObtenerTodos("Bruno", 2, 1, 20, CancellationToken.None);
        Assert.Equal("Bruno", repo.UltimaBusqueda);
        Assert.Equal((byte)2, repo.UltimoEstado);
    }

    [Fact]
    public async Task ObtenerTodos_EstadoInvalido_Devuelve400()
    {
        var (controller, _, _, _) = Crear();
        var result = await controller.ObtenerTodos(null, 6, 1, 20, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ObtenerPorId_ExistenteY404()
    {
        var (controller, _, _, _) = Crear();
        Assert.IsType<OkObjectResult>(await controller.ObtenerPorId(1, CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await controller.ObtenerPorId(99, CancellationToken.None));
    }

    [Fact]
    public async Task CambiarEstado_SupervisorPermitido_ActualizaYAuditaAnteriorYNuevo()
    {
        var (controller, _, bitacora, permisos) = Crear(true, true);
        var result = await controller.CambiarEstado(1, new CambiarServicioEstadoRequestDto(2), CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var cambio = Assert.IsType<CambioEstadoServicioDto>(ok.Value);
        Assert.Equal((byte)1, cambio.EstadoAnterior);
        Assert.Equal((byte)2, cambio.EstadoNuevo);
        Assert.Single(bitacora.Registros);
        Assert.Contains("EstadoAnterior=1", bitacora.Registros[0].Detalle);
        Assert.Contains("EstadoNuevo=2", bitacora.Registros[0].Detalle);
        Assert.Equal(AccionPermiso.Editar, permisos.Consultas.Single().Accion);
    }

    [Fact]
    public async Task CambiarEstado_RolSinEditar_Devuelve403()
    {
        var (controller, _, bitacora, _) = Crear(true, false);
        var result = await controller.CambiarEstado(1, new CambiarServicioEstadoRequestDto(2), CancellationToken.None);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Empty(bitacora.Registros);
    }

    [Fact]
    public async Task CambiarEstado_Invalido_Devuelve400()
    {
        var (controller, _, _, _) = Crear();
        var result = await controller.CambiarEstado(1, new CambiarServicioEstadoRequestDto(6), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CambiarEstado_Inexistente_Devuelve404()
    {
        var (controller, _, bitacora, _) = Crear();
        var result = await controller.CambiarEstado(99, new CambiarServicioEstadoRequestDto(2), CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(result);
        Assert.Empty(bitacora.Registros);
    }

    [Fact]
    public async Task ObtenerResumen_DevuelveMetricasGlobales()
    {
        var (controller, _, _, _) = Crear();
        var result = Assert.IsType<OkObjectResult>(await controller.ObtenerResumen(CancellationToken.None));
        var resumen = Assert.IsType<ServicioResumenDto>(result.Value);
        Assert.Equal(3, resumen.Total);
        Assert.Equal(1, resumen.Normal);
        Assert.Equal(1, resumen.ParaCorte);
        Assert.Equal(1, resumen.Cortado);
    }
}
