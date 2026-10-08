using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using VisorDatosSIG.Api.Controllers;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Application.DTOs.Disponibilidad;
using VisorDatosSIG.Application.DTOs.Security;
using VisorDatosSIG.Application.Interfaces;
using Xunit;

namespace VisorDatosSIG.UnitTests;

public sealed class DisponibilidadCu19Tests
{
    private sealed class Repo : IDisponibilidadRepository
    {
        public IReadOnlyList<HorarioTrabajoDto>? Horarios { get; set; } = [];
        public ResultadoOperacion<HorarioTrabajoDto> HorarioResultado { get; set; } = ResultadoOperacion<HorarioTrabajoDto>.Exito(Horario());
        public IReadOnlyList<DisponibilidadEmpleadoDto> Disponibles { get; set; } = [];
        public PagedResult<AsignacionTrabajoResumenDto> Pagina { get; set; } = new();
        public AsignacionTrabajoDetalleDto? Detalle { get; set; } = Asignacion();
        public ResultadoOperacion<AsignacionTrabajoDetalleDto> AsignacionResultado { get; set; } = ResultadoOperacion<AsignacionTrabajoDetalleDto>.Exito(Asignacion());
        public Task<IReadOnlyList<HorarioTrabajoDto>?> ObtenerHorariosAsync(int id,CancellationToken ct=default)=>Task.FromResult(Horarios);
        public Task<ResultadoOperacion<HorarioTrabajoDto>> CrearHorarioAsync(int id,GuardarHorarioRequestDto dto,CancellationToken ct=default)=>Task.FromResult(HorarioResultado);
        public Task<ResultadoOperacion<HorarioTrabajoDto>> ActualizarHorarioAsync(int id,GuardarHorarioRequestDto dto,CancellationToken ct=default)=>Task.FromResult(HorarioResultado);
        public Task<ResultadoOperacion<HorarioTrabajoDto>> CambiarEstadoHorarioAsync(int id,bool activo,CancellationToken ct=default)=>Task.FromResult(HorarioResultado);
        public Task<IReadOnlyList<DisponibilidadEmpleadoDto>> ConsultarDisponibilidadAsync(DisponibilidadConsultaDto q,CancellationToken ct=default)=>Task.FromResult(Disponibles);
        public Task<PagedResult<AsignacionTrabajoResumenDto>> ObtenerAsignacionesAsync(AsignacionTrabajoConsultaDto q,CancellationToken ct=default)=>Task.FromResult(Pagina);
        public Task<AsignacionTrabajoDetalleDto?> ObtenerAsignacionAsync(int id,CancellationToken ct=default)=>Task.FromResult(Detalle);
        public Task<ResultadoOperacion<AsignacionTrabajoDetalleDto>> CrearAsignacionAsync(GuardarAsignacionTrabajoDto d,CancellationToken ct=default)=>Task.FromResult(AsignacionResultado);
        public Task<ResultadoOperacion<AsignacionTrabajoDetalleDto>> ActualizarAsignacionAsync(int id,GuardarAsignacionTrabajoDto d,CancellationToken ct=default)=>Task.FromResult(AsignacionResultado);
        public Task<ResultadoOperacion<AsignacionTrabajoDetalleDto>> CambiarEstadoAsignacionAsync(int id,string e,CancellationToken ct=default)=>Task.FromResult(AsignacionResultado);
    }
    private sealed class Permisos(bool permitido=true) : IPermisoService
    {
        public string? Ruta { get; private set; } public AccionPermiso Accion { get; private set; }
        public Task<bool> TienePermisoAsync(int id,string ruta,AccionPermiso accion,CancellationToken ct=default){Ruta=ruta;Accion=accion;return Task.FromResult(permitido);}
        public Task<bool> PuedeVerAsync(int id,string ruta,CancellationToken ct=default)=>TienePermisoAsync(id,ruta,AccionPermiso.Ver,ct);
        public Task<bool> TieneAccesoWebAsync(int id,CancellationToken ct=default)=>Task.FromResult(permitido);
        public Task<PermisosEfectivosDto> ObtenerPermisosAsync(int id,CancellationToken ct=default)=>throw new NotSupportedException();
    }
    private sealed class Bitacora(bool falla=false) : IBitacoraService
    {
        public List<BitacoraEntryDto> Entradas { get; }=[];
        public Task<bool> RegistrarAsync(BitacoraEntryDto e,CancellationToken ct=default){if(falla)throw new InvalidOperationException("audit");Entradas.Add(e);return Task.FromResult(true);}
        public Task<IReadOnlyList<BitacoraItemDto>> ObtenerHistorialAsync(int l=100,CancellationToken ct=default)=>Task.FromResult<IReadOnlyList<BitacoraItemDto>>([]);
        public Task<IReadOnlyList<BitacoraItemDto>> ObtenerHistorialAsync(string? m,string? x,int l=100,CancellationToken ct=default)=>Task.FromResult<IReadOnlyList<BitacoraItemDto>>([]);
    }

    [Fact] public void Controladores_UsanAuthorizeSinRoles()
    {
        foreach(var tipo in new[]{typeof(DisponibilidadController),typeof(AsignacionesTrabajoController)})
        {var a=Assert.Single(tipo.GetCustomAttributes(typeof(AuthorizeAttribute),true).Cast<AuthorizeAttribute>());Assert.Null(a.Roles);}
    }
    [Fact] public void Permiso_Cu19_RegistradoEnCatalogo()=>Assert.Contains(PermisoMenu.DisponibilidadPersonal,PermisoMenu.Todas);

    [Fact] public void Horario_Valido_NoTieneError()=>Assert.Null(DisponibilidadReglas.ValidarHorario(1,new(8,0),new(12,0)));
    [Theory] [InlineData(8,0,8,0)] [InlineData(12,0,8,0)]
    public void Horario_InicioMayorOIgualFin_EsInvalido(int hi,int mi,int hf,int mf)=>Assert.NotNull(DisponibilidadReglas.ValidarHorario(1,new(hi,mi),new(hf,mf)));
    [Theory] [InlineData(0)] [InlineData(8)]
    public void Horario_DiaFueraRango_EsInvalido(byte dia)=>Assert.NotNull(DisponibilidadReglas.ValidarHorario(dia,new(8,0),new(12,0)));
    [Fact] public void Horario_Duplicado_SeSolapa()=>Assert.True(DisponibilidadReglas.SeSolapan(new TimeOnly(8,0),new TimeOnly(12,0),new TimeOnly(8,0),new TimeOnly(12,0)));
    [Fact] public void Horario_ParcialmenteSolapado_SeDetecta()=>Assert.True(DisponibilidadReglas.SeSolapan(new TimeOnly(11,0),new TimeOnly(15,0),new TimeOnly(8,0),new TimeOnly(12,0)));
    [Fact] public void Horario_FranjasSeparadas_SePermiten()=>Assert.False(DisponibilidadReglas.SeSolapan(new TimeOnly(14,0),new TimeOnly(16,0),new TimeOnly(8,0),new TimeOnly(12,0)));
    [Fact] public void Horario_IntervalosContiguos_SePermiten()=>Assert.False(DisponibilidadReglas.SeSolapan(new TimeOnly(12,0),new TimeOnly(16,0),new TimeOnly(8,0),new TimeOnly(12,0)));
    [Fact] public async Task Horarios_Listar_DevuelveOk()=>Assert.IsType<OkObjectResult>(await Disponibilidad().ObtenerHorarios(1,default));
    [Fact] public async Task Horarios_EmpleadoInexistente_Devuelve404(){var r=new Repo{Horarios=null};Assert.IsType<NotFoundObjectResult>(await Disponibilidad(r).ObtenerHorarios(99,default));}
    [Fact] public async Task Horarios_CrearValido_Devuelve201()=>Assert.IsType<CreatedResult>(await Disponibilidad().CrearHorario(1,NuevoHorario(),default));
    [Theory] [InlineData("duplicado")] [InlineData("solapamiento")]
    public async Task Horarios_Conflicto_Devuelve409(string mensaje){var r=new Repo{HorarioResultado=ResultadoOperacion<HorarioTrabajoDto>.Conflicto(mensaje)};Assert.IsType<ConflictObjectResult>(await Disponibilidad(r).CrearHorario(1,NuevoHorario(),default));}
    [Fact] public async Task Horarios_Actualizar_ExcluyePropioCuandoRepositorioAcepta()=>Assert.IsType<OkObjectResult>(await Disponibilidad().ActualizarHorario(1,NuevoHorario(),default));
    [Fact] public async Task Horarios_ReactivarConConflicto_Devuelve409(){var r=new Repo{HorarioResultado=ResultadoOperacion<HorarioTrabajoDto>.Conflicto("solapa")};Assert.IsType<ConflictObjectResult>(await Disponibilidad(r).CambiarEstadoHorario(1,new(true),default));}

    [Theory]
    [InlineData(false,false,false,false,"Inactivo")]
    [InlineData(true,true,false,true,"Baja Parcial")]
    [InlineData(true,false,false,false,"Fuera de horario")]
    [InlineData(true,false,true,true,"En Servicio")]
    [InlineData(true,false,true,false,"Disponible")]
    public void Disponibilidad_RespetaPrioridad(bool activo,bool baja,bool horario,bool asignacion,string esperado)=>Assert.Equal(esperado,DisponibilidadReglas.CalcularEstado(activo,baja,horario,asignacion));
    [Theory] [InlineData("Finalizada")] [InlineData("Cancelada")]
    public void Asignacion_NoOcupante_NoBloquea(string estado)=>Assert.False(CatalogosDisponibilidad.Ocupa(estado));
    [Fact] public void MultiplesFranjas_UnaAplicable_ResultaDisponible()=>Assert.Equal("Disponible",DisponibilidadReglas.CalcularEstado(true,false,true,false));
    [Fact] public async Task Disponibilidad_SinFechaHora_Devuelve400()=>Assert.IsType<BadRequestObjectResult>(await Disponibilidad().Consultar(null,null,null,null,null,null,default));
    [Fact] public async Task Disponibilidad_ConsultaValida_Devuelve200()=>Assert.IsType<OkObjectResult>(await Disponibilidad().Consultar(new DateOnly(2026,10,6),new TimeOnly(10,30),null,null,null,null,default));

    [Fact] public void Asignacion_Valida_Pasa()=>Assert.Null(DisponibilidadReglas.ValidarAsignacion(NuevaAsignacion()));
    [Fact] public void Asignacion_IdCodigoNull_EsPermitido()=>Assert.Null(DisponibilidadReglas.ValidarAsignacion(NuevaAsignacion() with{IdCodigo=null}));
    [Fact] public void Asignacion_FechaInvalida_SeRechaza()=>Assert.NotNull(DisponibilidadReglas.ValidarAsignacion(NuevaAsignacion() with{FechaFin=NuevaAsignacion().FechaInicio}));
    [Theory] [InlineData("NoExiste","Asignada","Normal")] [InlineData("Corte","NoExiste","Normal")] [InlineData("Corte","Asignada","NoExiste")]
    public void Asignacion_CatalogoInvalido_SeRechaza(string tipo,string estado,string prioridad)=>Assert.NotNull(DisponibilidadReglas.ValidarAsignacion(NuevaAsignacion() with{TipoTarea=tipo,Estado=estado,Prioridad=prioridad}));
    [Fact] public void Asignaciones_Solapadas_SeDetectan()=>Assert.True(DisponibilidadReglas.SeSolapan(Fecha(10),Fecha(12),Fecha(11),Fecha(13)));
    [Fact] public void Asignaciones_Contiguas_SePermiten()=>Assert.False(DisponibilidadReglas.SeSolapan(Fecha(10),Fecha(11),Fecha(11),Fecha(12)));
    [Fact] public async Task Asignacion_CrearValida_Devuelve201()=>Assert.IsType<CreatedAtActionResult>(await Asignaciones().Crear(NuevaAsignacion(),default));
    [Theory] [InlineData("empleado")] [InlineData("código fijo")]
    public async Task Asignacion_EntidadInexistente_Devuelve404(string mensaje){var r=new Repo{AsignacionResultado=ResultadoOperacion<AsignacionTrabajoDetalleDto>.NoEncontrado(mensaje)};Assert.IsType<NotFoundObjectResult>(await Asignaciones(r).Crear(NuevaAsignacion(),default));}
    [Theory] [InlineData("empleado inactivo")] [InlineData("fuera de horario")] [InlineData("atraviesa descanso")] [InlineData("baja parcial")] [InlineData("asignación solapada")]
    public async Task Asignacion_Conflictos_Devuelven409(string mensaje){var r=new Repo{AsignacionResultado=ResultadoOperacion<AsignacionTrabajoDetalleDto>.Conflicto(mensaje)};Assert.IsType<ConflictObjectResult>(await Asignaciones(r).Crear(NuevaAsignacion(),default));}
    [Fact] public async Task Asignacion_ActualizarExcluyendoPropia_Devuelve200()=>Assert.IsType<OkObjectResult>(await Asignaciones().Actualizar(1,NuevaAsignacion(),default));
    [Fact] public async Task Asignacion_CambiarEstado_Devuelve200()=>Assert.IsType<OkObjectResult>(await Asignaciones().CambiarEstado(1,new("Finalizada"),default));
    [Fact] public async Task SinIdentidad_Devuelve401(){var c=Disponibilidad(autenticado:false);Assert.IsType<UnauthorizedResult>(await c.ObtenerHorarios(1,default));}
    [Fact] public async Task SinPermiso_Devuelve403(){var c=Disponibilidad(permisos:new(false));Assert.Equal(403,Assert.IsType<ObjectResult>(await c.ObtenerHorarios(1,default)).StatusCode);}
    [Fact] public async Task UsaPermisoDinamicoCorrecto(){var p=new Permisos();await Disponibilidad(permisos:p).CrearHorario(1,NuevoHorario(),default);Assert.Equal(PermisoMenu.DisponibilidadPersonal,p.Ruta);Assert.Equal(AccionPermiso.Crear,p.Accion);}
    [Fact] public async Task BitacoraFalla_NoBloqueaOperacion()=>Assert.IsType<CreatedResult>(await Disponibilidad(bitacora:new(true)).CrearHorario(1,NuevoHorario(),default));
    [Fact] public async Task Bitacora_RegistraModuloYAccion(){var b=new Bitacora();await Disponibilidad(bitacora:b).CrearHorario(1,NuevoHorario(),default);Assert.Equal("Seguimiento de Servicios",Assert.Single(b.Entradas).Modulo);Assert.Equal("Crear Horario de Trabajo",b.Entradas[0].Accion);}

    private static DisponibilidadController Disponibilidad(Repo? repo=null,Permisos? permisos=null,Bitacora? bitacora=null,bool autenticado=true)
    {var c=new DisponibilidadController(repo??new(),permisos??new(),bitacora??new(),NullLogger<DisponibilidadController>.Instance);Preparar(c,autenticado);return c;}
    private static AsignacionesTrabajoController Asignaciones(Repo? repo=null)
    {var c=new AsignacionesTrabajoController(repo??new(),new Permisos(),new Bitacora(),NullLogger<AsignacionesTrabajoController>.Instance);Preparar(c,true);return c;}
    private static void Preparar(ControllerBase c,bool auth){var claims=auth?[new Claim(JwtRegisteredClaimNames.Sub,"7")]:Array.Empty<Claim>();c.ControllerContext=new(){HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity(claims,auth?"test":null))}};}
    private static HorarioTrabajoDto Horario()=>new(1,1,1,new(8,0),new(12,0),true,DateTime.UtcNow,null);
    private static GuardarHorarioRequestDto NuevoHorario()=>new(1,new(8,0),new(12,0));
    private static DateTime Fecha(int hora)=>new(2026,10,6,hora,0,0);
    private static GuardarAsignacionTrabajoDto NuevaAsignacion()=>new(1,1,"Corte",Fecha(10),Fecha(11),"Asignada","Normal",null);
    private static AsignacionTrabajoDetalleDto Asignacion()=>new(1,1,"EMP-1","Carlos Mendoza",1,"CF-1","Cliente",-63,-17,"Corte",Fecha(10),Fecha(11),"Asignada","Normal",null,DateTime.UtcNow,null);
}
