using VisorDatosSIG.Application.DTOs.Bitacora;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Data;

namespace VisorDatosSIG.UnitTests;

/// <summary>
/// Pruebas de la normalización de la consulta de bitácora web (CU05) con un repositorio simulado,
/// sin SQL Server.
/// </summary>
/// <remarks>
/// El servicio es el único responsable de acotar lo que llega del cliente: página, tamaño de página,
/// longitudes de los filtros y rango de fechas (un rango invertido no se intercambia aquí: lo
/// rechaza con HTTP 400 la validación de <c>BitacoraConsultaDto</c>). La exclusión del módulo de
/// migración es responsabilidad del repositorio (se verifica en <c>BitacoraConsultaRepository</c>).
/// </remarks>
public sealed class BitacoraConsultaServiceTests
{
    private sealed class BitacoraConsultaRepositoryFalso : IBitacoraConsultaRepository
    {
        public BitacoraConsultaDto? ConsultaRecibida { get; private set; }

        public long? IdRecibido { get; private set; }

        public Task<BitacoraPaginaDto> ConsultarAsync(
            BitacoraConsultaDto consulta,
            CancellationToken cancellationToken = default)
        {
            ConsultaRecibida = consulta;

            return Task.FromResult(new BitacoraPaginaDto
            {
                Pagina = consulta.Pagina,
                Tamano = consulta.Tamano
            });
        }

        public Task<BitacoraCatalogosDto> ObtenerCatalogosAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new BitacoraCatalogosDto());

        public Task<BitacoraItemWebDto?> ObtenerPorIdAsync(
            long idBitacora,
            CancellationToken cancellationToken = default)
        {
            IdRecibido = idBitacora;

            return Task.FromResult<BitacoraItemWebDto?>(null);
        }
    }

    [Fact(DisplayName = "Bitácora: la página y el tamaño fuera de rango se ajustan antes de consultar")]
    public async Task AjustaPaginaYTamano()
    {
        var repositorio = new BitacoraConsultaRepositoryFalso();
        var servicio = new BitacoraConsultaService(repositorio);

        await servicio.ConsultarAsync(new BitacoraConsultaDto { Pagina = 0, Tamano = 5000 });

        Assert.Equal(BitacoraConsultaDto.PaginaPredeterminada, repositorio.ConsultaRecibida!.Pagina);
        Assert.Equal(BitacoraConsultaDto.TamanoMaximo, repositorio.ConsultaRecibida.Tamano);
    }

    [Fact(DisplayName = "Bitácora: un tamaño no informado usa el valor predeterminado")]
    public async Task TamanoNoInformadoUsaElPredeterminado()
    {
        var repositorio = new BitacoraConsultaRepositoryFalso();

        await new BitacoraConsultaService(repositorio).ConsultarAsync(new BitacoraConsultaDto { Tamano = 0 });

        Assert.Equal(BitacoraConsultaDto.TamanoPredeterminado, repositorio.ConsultaRecibida!.Tamano);
    }

    [Fact(DisplayName = "Bitácora: los textos se recortan, los vacíos quedan nulos y el usuario 0 se descarta")]
    public async Task NormalizaLosTextos()
    {
        var repositorio = new BitacoraConsultaRepositoryFalso();

        await new BitacoraConsultaService(repositorio).ConsultarAsync(new BitacoraConsultaDto
        {
            Buscar = new string('x', 500),
            Modulo = "   ",
            Accion = "  Inicio de Sesión  ",
            IdUsuario = 0
        });

        var consulta = repositorio.ConsultaRecibida!;

        Assert.Equal(BitacoraConsultaDto.LongitudMaximaBusqueda, consulta.Buscar!.Length);
        Assert.Null(consulta.Modulo);
        Assert.Equal("Inicio de Sesión", consulta.Accion);
        Assert.Null(consulta.IdUsuario);
    }

    [Fact(DisplayName = "Bitácora: un rango invertido no se intercambia y la validación del DTO lo rechaza")]
    public async Task NoIntercambiaElRangoDeFechas()
    {
        var repositorio = new BitacoraConsultaRepositoryFalso();
        var desde = new DateTime(2026, 5, 10, 8, 0, 0, DateTimeKind.Unspecified);
        var hasta = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Unspecified);
        var solicitada = new BitacoraConsultaDto { FechaDesde = desde, FechaHasta = hasta };

        await new BitacoraConsultaService(repositorio).ConsultarAsync(solicitada);

        var consulta = repositorio.ConsultaRecibida!;

        // El servicio no reordena las fechas: un rango invertido llega tal cual al repositorio y
        // quien produce el HTTP 400 es la validación del DTO (BitacoraConsultaDto : IValidatableObject).
        Assert.Equal(desde, consulta.FechaDesde);
        Assert.Equal(hasta, consulta.FechaHasta);

        var errores = solicitada
            .Validate(new System.ComponentModel.DataAnnotations.ValidationContext(solicitada))
            .ToList();

        Assert.Single(errores);
    }

    [Fact(DisplayName = "Bitácora: un identificador inválido no consulta el repositorio")]
    public async Task IdInvalidoNoConsulta()
    {
        var repositorio = new BitacoraConsultaRepositoryFalso();

        Assert.Null(await new BitacoraConsultaService(repositorio).ObtenerPorIdAsync(0));
        Assert.Null(repositorio.IdRecibido);

        Assert.Null(await new BitacoraConsultaService(repositorio).ObtenerPorIdAsync(15));
        Assert.Equal(15, repositorio.IdRecibido);
    }
}
