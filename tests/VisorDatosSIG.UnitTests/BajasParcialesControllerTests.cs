using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using VisorDatosSIG.Api.Controllers;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Application.DTOs.BajasParciales;
using VisorDatosSIG.Application.DTOs.Empleados;
using VisorDatosSIG.Application.DTOs.Security;
using VisorDatosSIG.Application.Interfaces;
using Xunit;

namespace VisorDatosSIG.UnitTests;

public sealed class BajasParcialesControllerTests
{
    private sealed class FakeBajaRepository : IBajaParcialRepository
    {
        public List<BajaParcialResponseDto> Items { get; } = [];
        public List<BajaParcialEmpleadoCatalogoDto> Empleados { get; } =
        [new(1, "EMP-001", "Ana Prueba", "Disponible", true)];
        public bool Solapamiento { get; set; }
        public int Sincronizaciones { get; private set; }
        public string? UltimoEstado { get; private set; }

        public Task<IReadOnlyList<BajaParcialEmpleadoCatalogoDto>> ObtenerEmpleadosCatalogoAsync(
            CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BajaParcialEmpleadoCatalogoDto>>(Empleados);

        public Task<IEnumerable<BajaParcialResponseDto>> ObtenerTodosAsync(
            int? idEmpleado = null, string? estado = null, CancellationToken ct = default) =>
            Task.FromResult<IEnumerable<BajaParcialResponseDto>>(Items
                .Where(x => (!idEmpleado.HasValue || x.IdEmpleado == idEmpleado) &&
                            (string.IsNullOrWhiteSpace(estado) || x.Estado == estado))
                .ToList());

        public Task<BajaParcialResponseDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.IdBajaParcial == id));

        public Task<BajaParcialResponseDto?> CrearAsync(CreateBajaParcialRequestDto dto, CancellationToken ct = default)
        {
            if (Solapamiento)
            {
                throw new VisorDatosSIG.Application.Exceptions.BajaParcialSolapamientoException();
            }

            var item = CrearItem(Items.Count + 1, dto.IdEmpleado, dto.FechaInicio, dto.FechaFin, "Activa");
            Items.Add(item);
            return Task.FromResult<BajaParcialResponseDto?>(item);
        }

        public Task<BajaParcialResponseDto?> ActualizarAsync(int id, UpdateBajaParcialRequestDto dto, CancellationToken ct = default)
        {
            if (Solapamiento)
            {
                throw new VisorDatosSIG.Application.Exceptions.BajaParcialSolapamientoException();
            }

            var index = Items.FindIndex(x => x.IdBajaParcial == id);
            if (index < 0) return Task.FromResult<BajaParcialResponseDto?>(null);
            var updated = Items[index] with
            {
                FechaInicio = dto.FechaInicio,
                FechaFin = dto.FechaFin,
                Motivo = dto.Motivo,
                Observaciones = dto.Observaciones
            };
            Items[index] = updated;
            return Task.FromResult<BajaParcialResponseDto?>(updated);
        }

        public Task<bool> CambiarEstadoAsync(int id, string estado, CancellationToken ct = default)
        {
            if (Solapamiento && estado == "Activa")
            {
                throw new VisorDatosSIG.Application.Exceptions.BajaParcialSolapamientoException();
            }

            UltimoEstado = estado;
            var index = Items.FindIndex(x => x.IdBajaParcial == id);
            if (index < 0) return Task.FromResult(false);
            Items[index] = Items[index] with { Estado = estado };
            return Task.FromResult(true);
        }

        public Task<bool> ExisteSolapamientoAsync(int idEmpleado, DateTime inicio, DateTime fin,
            int? ignorarIdBajaParcial = null, CancellationToken ct = default) =>
            Task.FromResult(Solapamiento);

        public Task<int> SincronizarVigenciasAsync(DateTime fechaReferencia, CancellationToken ct = default)
        {
            Sincronizaciones++;
            return Task.FromResult(0);
        }

        private static BajaParcialResponseDto CrearItem(int id, int empleado, DateTime inicio, DateTime fin, string estado) =>
            new(id, empleado, "EMP-001", "Ana Prueba", inicio, fin, "Motivo válido", null,
                estado, DateTime.UtcNow, null);
    }

    private sealed class FakeEmpleadoRepository : IEmpleadoRepository
    {
        public EmpleadoResponseDto? Empleado { get; set; } = EmpleadoValido(1);

        public Task<IEnumerable<EmpleadoResponseDto>> ObtenerTodosAsync(string? busqueda = null, string? cargo = null,
            string? disponibilidad = null, bool? activo = null, CancellationToken ct = default) =>
            Task.FromResult<IEnumerable<EmpleadoResponseDto>>(Empleado is null ? [] : [Empleado]);
        public Task<EmpleadoResponseDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Empleado);
        public Task<EmpleadoResponseDto?> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default) => Task.FromResult(Empleado);
        public Task<EmpleadoResponseDto?> CrearAsync(CreateEmpleadoRequestDto dto, CancellationToken ct = default) => Task.FromResult<EmpleadoResponseDto?>(Empleado);
        public Task<EmpleadoResponseDto?> ActualizarAsync(int id, UpdateEmpleadoRequestDto dto, CancellationToken ct = default) => Task.FromResult<EmpleadoResponseDto?>(Empleado);
        public Task<bool> CambiarEstadoAsync(int id, bool activo, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> CambiarDisponibilidadAsync(int id, string disponibilidad, string? motivo = null, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> ExisteCodigoAsync(string codigo, int? ignorarId = null, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> ExisteDocumentoAsync(string documento, int? ignorarId = null, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<string>> ObtenerCargosAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<string>> ObtenerDisponibilidadesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<EmpleadoMetricasDto> ObtenerMetricasAsync(CancellationToken ct = default) => Task.FromResult(new EmpleadoMetricasDto(1, 1, 0, 1, 0, 0, 0));

        private static EmpleadoResponseDto EmpleadoValido(int id) => new(id, "EMP-001", "Ana", "Prueba", "Ana Prueba",
            "123", null, null, "Técnico", "Operaciones", "Disponible", true, DateTime.UtcNow, null, null);
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

    private sealed class FakePermisoService(bool puede) : IPermisoService
    {
        public Task<PermisosEfectivosDto> ObtenerPermisosAsync(int idUsuario, CancellationToken ct = default) =>
            Task.FromResult(new PermisosEfectivosDto { TieneAccesoWeb = puede, AccesoTotal = puede });
        public Task<bool> TieneAccesoWebAsync(int idUsuario, CancellationToken ct = default) => Task.FromResult(puede);
        public Task<bool> TienePermisoAsync(int idUsuario, string menuUrl, AccionPermiso accion, CancellationToken ct = default) => Task.FromResult(puede);
        public Task<bool> PuedeVerAsync(int idUsuario, string menuUrl, CancellationToken ct = default) => Task.FromResult(puede);
    }

    private static BajasParcialesController CrearControlador(
    FakeBajaRepository bajas, FakeEmpleadoRepository empleados, bool puede = true)
    {
        var controller = new BajasParcialesController(
            bajas, empleados, new FakeBitacoraRepository(), new FakePermisoService(puede),
            NullLogger<BajasParcialesController>.Instance);
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "7"),
                new Claim("sub", "7")
            ],
            "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    private static CreateBajaParcialRequestDto CrearDto(DateTime? inicio = null, DateTime? fin = null) =>
        new(1, inicio ?? DateTime.Today, fin ?? DateTime.Today.AddDays(2), "Motivo válido", null);

    [Fact]
    public async Task ObtenerTodos_Autorizado_SincronizaYDevuelveOk()
    {
        var bajas = new FakeBajaRepository();
        var result = await CrearControlador(bajas, new FakeEmpleadoRepository()).ObtenerTodos(null, null, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, bajas.Sincronizaciones);
    }

    [Fact]
    public async Task ObtenerEmpleadosCatalogo_Autorizado_DevuelveListadoMinimo()
    {
        var result = await CrearControlador(new FakeBajaRepository(), new FakeEmpleadoRepository())
            .ObtenerEmpleadosCatalogo(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var empleados = Assert.IsAssignableFrom<IReadOnlyList<BajaParcialEmpleadoCatalogoDto>>(ok.Value);
        Assert.Single(empleados);
        Assert.Equal("EMP-001", empleados[0].Codigo);
    }

    [Fact]
    public async Task ObtenerEmpleadosCatalogo_SinPermiso_Devuelve403()
    {
        var result = await CrearControlador(new FakeBajaRepository(), new FakeEmpleadoRepository(), false)
            .ObtenerEmpleadosCatalogo(CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task ObtenerTodos_SinPermiso_Devuelve403()
    {
        var bajas = new FakeBajaRepository();
        var result = await CrearControlador(bajas, new FakeEmpleadoRepository(), false).ObtenerTodos(null, null, CancellationToken.None);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal(0, bajas.Sincronizaciones);
    }

    [Fact]
    public async Task Crear_FechaInvalida_Devuelve400()
    {
        var result = await CrearControlador(new FakeBajaRepository(), new FakeEmpleadoRepository())
            .Crear(CrearDto(DateTime.Today.AddDays(2), DateTime.Today), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Crear_EmpleadoInexistenteOInactivo_DevuelveElResultadoCorrecto()
    {
        var empleados = new FakeEmpleadoRepository { Empleado = null };
        var notFound = await CrearControlador(new FakeBajaRepository(), empleados).Crear(CrearDto(), CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(notFound);

        empleados.Empleado = empleados.GetType() is not null
            ? new EmpleadoResponseDto(1, "EMP-001", "Ana", "Prueba", "Ana Prueba", "123", null, null,
                "Técnico", "Operaciones", "Disponible", false, DateTime.UtcNow, null, null)
            : null;
        var inactive = await CrearControlador(new FakeBajaRepository(), empleados).Crear(CrearDto(), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(inactive);
    }

    [Fact]
    public async Task Crear_SolapamientoConcurrente_Devuelve400()
    {
        var bajas = new FakeBajaRepository { Solapamiento = true };
        var result = await CrearControlador(bajas, new FakeEmpleadoRepository()).Crear(CrearDto(), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CrearYActualizar_Validos_AuditanYDevuelvenOk()
    {
        var bajas = new FakeBajaRepository();
        var controller = CrearControlador(bajas, new FakeEmpleadoRepository());
        var created = Assert.IsType<CreatedAtActionResult>(await controller.Crear(CrearDto(), CancellationToken.None));
        var updated = await controller.Actualizar(1, new UpdateBajaParcialRequestDto(
            DateTime.Today.AddDays(1), DateTime.Today.AddDays(3), "Motivo actualizado", "Observación"), CancellationToken.None);
        Assert.NotNull(created.Value);
        Assert.IsType<OkObjectResult>(updated);
    }

    [Theory]
    [InlineData("Finalizada")]
    [InlineData("Cancelada")]
    public async Task CambiarEstado_FinalizadaOCancelada_DevuelveOk(string estado)
    {
        var bajas = new FakeBajaRepository();
        bajas.Items.Add(new(1, 1, "EMP-001", "Ana Prueba", DateTime.Today, DateTime.Today.AddDays(1),
            "Motivo", null, "Activa", DateTime.UtcNow, null));
        var result = await CrearControlador(bajas, new FakeEmpleadoRepository()).CambiarEstado(
            1, new ChangeBajaParcialEstadoDto(estado), CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(estado, bajas.UltimoEstado);
    }

    [Fact]
    public async Task ReactivarConSolapamiento_Devuelve400()
    {
        var bajas = new FakeBajaRepository { Solapamiento = true };
        bajas.Items.Add(new(1, 1, "EMP-001", "Ana Prueba", DateTime.Today, DateTime.Today.AddDays(1),
            "Motivo", null, "Finalizada", DateTime.UtcNow, null));
        var result = await CrearControlador(bajas, new FakeEmpleadoRepository()).CambiarEstado(
            1, new ChangeBajaParcialEstadoDto("Activa"), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }
}
