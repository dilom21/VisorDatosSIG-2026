using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using VisorDatosSIG.Api.Controllers;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Application.DTOs.Empleados;
using VisorDatosSIG.Application.DTOs.Security;
using VisorDatosSIG.Application.Interfaces;
using Xunit;

namespace VisorDatosSIG.UnitTests;

public sealed class GestionEmpleadosTests
{
    private sealed class FakeEmpleadoRepository : IEmpleadoRepository
    {
        public bool Exito { get; set; } = true;
        public bool CodigoExiste { get; set; } = false;
        public bool DocumentoExiste { get; set; } = false;
        public int? UltimoIdAfectado { get; private set; }
        public CreateEmpleadoRequestDto? UltimoCreado { get; private set; }
        public UpdateEmpleadoRequestDto? UltimoActualizado { get; private set; }
        public string? UltimaDisponibilidad { get; private set; }
        public bool? UltimoEstado { get; private set; }

        private static EmpleadoResponseDto MockEmpleado(int id) => new(
            id,
            "EMP-001",
            "Carlos",
            "Mendoza",
            "Carlos Mendoza",
            "7845123 SC",
            "70012345",
            "carlos@sig.com",
            "Técnico de Campo",
            "Operaciones",
            "Disponible",
            true,
            DateTime.UtcNow,
            null,
            "Personal asignado");

        public Task<IEnumerable<EmpleadoResponseDto>> ObtenerTodosAsync(
            string? busqueda = null, string? cargo = null, string? disponibilidad = null, bool? activo = null, CancellationToken ct = default) =>
            Task.FromResult<IEnumerable<EmpleadoResponseDto>>([MockEmpleado(1)]);

        public Task<EmpleadoResponseDto?> ObtenerPorIdAsync(int idEmpleado, CancellationToken ct = default) =>
            Task.FromResult<EmpleadoResponseDto?>(Exito ? MockEmpleado(idEmpleado) : null);

        public Task<EmpleadoResponseDto?> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default) =>
            Task.FromResult<EmpleadoResponseDto?>(Exito ? MockEmpleado(1) with { Codigo = codigo } : null);

        public Task<EmpleadoResponseDto?> CrearAsync(CreateEmpleadoRequestDto dto, CancellationToken ct = default)
        {
            UltimoCreado = dto;
            return Task.FromResult<EmpleadoResponseDto?>(Exito ? MockEmpleado(10) with
            {
                Codigo = dto.Codigo,
                Nombres = dto.Nombres,
                Apellidos = dto.Apellidos,
                NombreCompleto = $"{dto.Nombres} {dto.Apellidos}",
                DocumentoIdentidad = dto.DocumentoIdentidad,
                Cargo = dto.Cargo,
                Area = dto.Area,
                Disponibilidad = dto.Disponibilidad
            } : null);
        }

        public Task<EmpleadoResponseDto?> ActualizarAsync(int idEmpleado, UpdateEmpleadoRequestDto dto, CancellationToken ct = default)
        {
            UltimoIdAfectado = idEmpleado;
            UltimoActualizado = dto;
            return Task.FromResult<EmpleadoResponseDto?>(Exito ? MockEmpleado(idEmpleado) with
            {
                Nombres = dto.Nombres,
                Apellidos = dto.Apellidos,
                DocumentoIdentidad = dto.DocumentoIdentidad,
                Cargo = dto.Cargo,
                Area = dto.Area,
                Disponibilidad = dto.Disponibilidad
            } : null);
        }

        public Task<bool> CambiarEstadoAsync(int idEmpleado, bool activo, CancellationToken ct = default)
        {
            UltimoIdAfectado = idEmpleado;
            UltimoEstado = activo;
            return Task.FromResult(Exito);
        }

        public Task<bool> CambiarDisponibilidadAsync(int idEmpleado, string disponibilidad, string? motivo = null, CancellationToken ct = default)
        {
            UltimoIdAfectado = idEmpleado;
            UltimaDisponibilidad = disponibilidad;
            return Task.FromResult(Exito);
        }

        public Task<bool> ExisteCodigoAsync(string codigo, int? ignorarId = null, CancellationToken ct = default) =>
            Task.FromResult(CodigoExiste);

        public Task<bool> ExisteDocumentoAsync(string documento, int? ignorarId = null, CancellationToken ct = default) =>
            Task.FromResult(DocumentoExiste);

        public Task<IReadOnlyList<string>> ObtenerCargosAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(["Técnico de Campo", "Supervisor de Zona"]);

        public Task<IReadOnlyList<string>> ObtenerDisponibilidadesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(["Disponible", "En Servicio", "Baja Parcial"]);

        public Task<EmpleadoMetricasDto> ObtenerMetricasAsync(CancellationToken ct = default) =>
            Task.FromResult(new EmpleadoMetricasDto(10, 8, 2, 5, 3, 1, 1));
    }

    private sealed class FakeBitacoraRepository : IBitacoraRepository
    {
        public List<BitacoraRegistroDto> Registros { get; } = [];
        public Task RegistrarAsync(BitacoraRegistroDto dto, CancellationToken ct = default)
        {
            Registros.Add(dto);
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

    private static EmpleadosController CrearControlador(
        IEmpleadoRepository repo,
        IBitacoraRepository bitacora,
        bool puede,
        string? sub = "1",
        string rol = "Administrador")
    {
        var permisoService = new FakePermisoService(puede);
        var controller = new EmpleadosController(repo, bitacora, permisoService, NullLogger<EmpleadosController>.Instance);

        var claims = new List<Claim>();
        if (!string.IsNullOrEmpty(sub))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, sub));
            claims.Add(new Claim("sub", sub));
            claims.Add(new Claim(ClaimTypes.Role, rol));
            claims.Add(new Claim("role", rol));
        }

        var identity = claims.Count > 0 ? new ClaimsIdentity(claims, "TestAuth") : new ClaimsIdentity();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        return controller;
    }

    [Fact]
    public async Task ObtenerTodos_ConPermiso_RetornaOkConLista()
    {
        var repo = new FakeEmpleadoRepository();
        var bitacora = new FakeBitacoraRepository();
        var ctrl = CrearControlador(repo, bitacora, puede: true);

        var res = await ctrl.ObtenerTodos(null, null, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(res);
        var items = Assert.IsAssignableFrom<IEnumerable<EmpleadoResponseDto>>(ok.Value);
        Assert.Single(items);
    }

    [Fact]
    public async Task ObtenerTodos_SinPermiso_RetornaForbidden()
    {
        var repo = new FakeEmpleadoRepository();
        var bitacora = new FakeBitacoraRepository();
        var ctrl = CrearControlador(repo, bitacora, puede: false, rol: "Consultor");

        var res = await ctrl.ObtenerTodos(null, null, null, null, CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(res);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
    }

    [Fact]
    public async Task ObtenerPorId_Existente_RetornaOk()
    {
        var repo = new FakeEmpleadoRepository();
        var bitacora = new FakeBitacoraRepository();
        var ctrl = CrearControlador(repo, bitacora, puede: true);

        var res = await ctrl.ObtenerPorId(1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(res);
        var emp = Assert.IsType<EmpleadoResponseDto>(ok.Value);
        Assert.Equal(1, emp.IdEmpleado);
        Assert.Equal("EMP-001", emp.Codigo);
    }

    [Fact]
    public async Task ObtenerPorId_Inexistente_RetornaNotFound()
    {
        var repo = new FakeEmpleadoRepository { Exito = false };
        var bitacora = new FakeBitacoraRepository();
        var ctrl = CrearControlador(repo, bitacora, puede: true);

        var res = await ctrl.ObtenerPorId(999, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(res);
    }

    [Fact]
    public async Task Crear_DatosValidos_RetornaCreatedYAuditaEnBitacora()
    {
        var repo = new FakeEmpleadoRepository();
        var bitacora = new FakeBitacoraRepository();
        var ctrl = CrearControlador(repo, bitacora, puede: true);

        var dto = new CreateEmpleadoRequestDto(
            "EMP-010",
            "Pedro",
            "Alvarado",
            "9874561 SC",
            "70112233",
            "pedro@sig.com",
            "Técnico de Campo",
            "Operaciones",
            "Disponible",
            "Prueba de alta");

        var res = await ctrl.Crear(dto, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(res);
        var emp = Assert.IsType<EmpleadoResponseDto>(created.Value);
        Assert.Equal("EMP-010", emp.Codigo);
        Assert.Equal("Pedro Alvarado", emp.NombreCompleto);

        Assert.Single(bitacora.Registros);
        var log = bitacora.Registros[0];
        Assert.Equal("Seguimiento de Servicios", log.Modulo);
        Assert.Equal("ALTA_EMPLEADO", log.Accion);
        Assert.Equal("Empleados", log.Entidad);
    }

    [Fact]
    public async Task Crear_CodigoDuplicado_RetornaBadRequest()
    {
        var repo = new FakeEmpleadoRepository { CodigoExiste = true };
        var bitacora = new FakeBitacoraRepository();
        var ctrl = CrearControlador(repo, bitacora, puede: true);

        var dto = new CreateEmpleadoRequestDto(
            "EMP-001",
            "Pedro",
            "Alvarado",
            "9874561 SC",
            null,
            null,
            "Técnico de Campo",
            "Operaciones",
            "Disponible",
            null);

        var res = await ctrl.Crear(dto, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(res);
        Assert.Contains("código de empleado", bad.Value?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Empty(bitacora.Registros);
    }

    [Fact]
    public async Task Actualizar_Existente_RetornaOkYAudita()
    {
        var repo = new FakeEmpleadoRepository();
        var bitacora = new FakeBitacoraRepository();
        var ctrl = CrearControlador(repo, bitacora, puede: true);

        var dto = new UpdateEmpleadoRequestDto(
            "Carlos Eduardo",
            "Mendoza Flores",
            "7845123 SC",
            "70012345",
            "carlos.mendoza@sig.com",
            "Supervisor de Zona",
            "Seguimiento de Servicios",
            "En Servicio",
            "Actualizado");

        var res = await ctrl.Actualizar(1, dto, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(res);
        var emp = Assert.IsType<EmpleadoResponseDto>(ok.Value);
        Assert.Equal("Carlos Eduardo", emp.Nombres);

        Assert.Single(bitacora.Registros);
        Assert.Equal("ACTUALIZACION_EMPLEADO", bitacora.Registros[0].Accion);
    }

    [Fact]
    public async Task CambiarEstado_BajaLogica_RetornaOkYAudita()
    {
        var repo = new FakeEmpleadoRepository();
        var bitacora = new FakeBitacoraRepository();
        var ctrl = CrearControlador(repo, bitacora, puede: true);

        var res = await ctrl.CambiarEstado(1, new ChangeEmpleadoEstadoDto(false), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(res);
        Assert.False(repo.UltimoEstado);
        Assert.Single(bitacora.Registros);
        Assert.Equal("BAJA_LOGICA_EMPLEADO", bitacora.Registros[0].Accion);
    }

    [Fact]
    public async Task CambiarDisponibilidad_BajaParcialConMotivo_RetornaOkYAudita()
    {
        var repo = new FakeEmpleadoRepository();
        var bitacora = new FakeBitacoraRepository();
        var ctrl = CrearControlador(repo, bitacora, puede: true);

        var dto = new ChangeEmpleadoDisponibilidadDto("Baja Parcial", "Permiso de capacitación técnica de campo");
        var res = await ctrl.CambiarDisponibilidad(1, dto, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(res);
        Assert.Equal("Baja Parcial", repo.UltimaDisponibilidad);
        Assert.Single(bitacora.Registros);
        Assert.Equal("CAMBIO_DISPONIBILIDAD_EMPLEADO", bitacora.Registros[0].Accion);
    }

    [Fact]
    public async Task Supervisor_ConPermiso_PuedeGestionarEmpleados()
    {
        var repo = new FakeEmpleadoRepository();
        var bitacora = new FakeBitacoraRepository();
        // Supervisor tiene permiso otorgado en RolMenu
        var ctrl = CrearControlador(repo, bitacora, puede: true, rol: "Supervisor");

        var res = await ctrl.ObtenerMetricas(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(res);
        var metricas = Assert.IsType<EmpleadoMetricasDto>(ok.Value);
        Assert.Equal(10, metricas.Total);
        Assert.Equal(5, metricas.Disponibles);
    }
}
