using System.Net;
using System.Text;
using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Migrador.Services;
using VisorDatosSIG.Migrador.Session;

namespace VisorDatosSIG.Migrador.UnitTests;

/// <summary>
/// Pruebas del cliente HTTP de autenticación del Migrador: inicio de sesión, consulta del usuario
/// y cierre de sesión, incluyendo errores de red y sesión vencida.
/// </summary>
/// <remarks>
/// Se usa un manejador de mensajes falso: no se requiere la API ni SQL Server en ejecución.
/// </remarks>
public sealed class AuthenticationApiClientTests
{
    private const string Token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.carga.firma";

    private const string RespuestaLogin =
        """
        {
          "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.carga.firma",
          "expiresIn": 1800,
          "usuario": {
            "idUsuario": 1,
            "login": "admin",
            "nombre": "Administrador",
            "roles": ["Administrador"]
          }
        }
        """;

    private const string RespuestaUsuario =
        """
        {
          "idUsuario": 3,
          "login": "consultor",
          "nombre": "Consultor",
          "roles": ["Consultor"]
        }
        """;

    private static AuthenticationApiClient CrearCliente(HttpMessageHandler manejador) =>
        new(new HttpClient(manejador) { BaseAddress = new Uri("http://localhost:5080/") });

    private static UserSession CrearSesionAutenticada()
    {
        var sesion = new UserSession();
        sesion.Establecer(
            Token,
            new AuthenticatedUserDto
            {
                IdUsuario = 1,
                Login = "admin",
                Nombre = "Administrador",
                Roles = ["Administrador"]
            },
            1800);
        return sesion;
    }

    [Fact(DisplayName = "Login 200: procesa el token y los datos del usuario")]
    public async Task Login200ProcesaElTokenYLosDatosDelUsuario()
    {
        var manejador = new ManejadorFalso(HttpStatusCode.OK, RespuestaLogin);
        var cliente = CrearCliente(manejador);

        var resultado = await cliente.IniciarSesionAsync("admin", "Admin123!");

        Assert.True(resultado.EsExitoso);
        Assert.Equal(EstadoOperacion.Exito, resultado.Estado);
        Assert.NotNull(resultado.Datos);
        Assert.Equal(Token, resultado.Datos!.AccessToken);
        Assert.Equal(1800, resultado.Datos.ExpiresIn);
        Assert.Equal(1, resultado.Datos.Usuario.IdUsuario);
        Assert.Equal("admin", resultado.Datos.Usuario.Login);
        Assert.Equal("Administrador", resultado.Datos.Usuario.Nombre);
        Assert.Equal(new[] { "Administrador" }, resultado.Datos.Usuario.Roles);
    }

    [Fact(DisplayName = "Login: la petición usa POST, JSON y no envía token")]
    public async Task LoginUsaPostJsonYSinToken()
    {
        var manejador = new ManejadorFalso(HttpStatusCode.OK, RespuestaLogin);
        var cliente = CrearCliente(manejador);

        await cliente.IniciarSesionAsync("admin", "Admin123!");

        Assert.Equal(HttpMethod.Post, manejador.Metodo);
        Assert.Equal("/api/autenticacion/iniciar", manejador.Ruta);
        Assert.Equal("application/json", manejador.TipoContenido);
        Assert.Null(manejador.Autorizacion);
        Assert.Contains("\"login\":\"admin\"", manejador.Cuerpo!, StringComparison.Ordinal);
        Assert.Contains("\"password\":\"Admin123!\"", manejador.Cuerpo!, StringComparison.Ordinal);
        Assert.DoesNotContain("IdUsuario", manejador.Cuerpo!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Login 401: credenciales incorrectas con mensaje para el usuario")]
    public async Task Login401DevuelveCredencialesInvalidas()
    {
        var cliente = CrearCliente(new ManejadorFalso(HttpStatusCode.Unauthorized, "{}"));

        var resultado = await cliente.IniciarSesionAsync("admin", "incorrecta");

        Assert.False(resultado.EsExitoso);
        Assert.Equal(EstadoOperacion.CredencialesInvalidas, resultado.Estado);
        Assert.Equal(MensajesAutenticacion.CredencialesInvalidas, resultado.Mensaje);
        Assert.Null(resultado.Datos);
    }

    [Fact(DisplayName = "Login 403: cuenta deshabilitada")]
    public async Task Login403DevuelveCuentaInhabilitada()
    {
        var cliente = CrearCliente(new ManejadorFalso(HttpStatusCode.Forbidden, "{}"));

        var resultado = await cliente.IniciarSesionAsync("admin", "Admin123!");

        Assert.Equal(EstadoOperacion.CuentaInhabilitada, resultado.Estado);
        Assert.Equal(MensajesAutenticacion.CuentaInhabilitada, resultado.Mensaje);
    }

    [Fact(DisplayName = "Login 400: solicitud inválida")]
    public async Task Login400DevuelveSolicitudInvalida()
    {
        var cliente = CrearCliente(new ManejadorFalso(HttpStatusCode.BadRequest, "{}"));

        var resultado = await cliente.IniciarSesionAsync(string.Empty, string.Empty);

        Assert.Equal(EstadoOperacion.SolicitudInvalida, resultado.Estado);
        Assert.Equal(MensajesAutenticacion.SolicitudInvalida, resultado.Mensaje);
    }

    [Fact(DisplayName = "Login 500: error del servidor sin detalles internos")]
    public async Task Login500DevuelveErrorDelServidor()
    {
        var cliente = CrearCliente(new ManejadorFalso(HttpStatusCode.InternalServerError, "detalle interno"));

        var resultado = await cliente.IniciarSesionAsync("admin", "Admin123!");

        Assert.Equal(EstadoOperacion.ErrorServidor, resultado.Estado);
        Assert.Equal(MensajesAutenticacion.ErrorServidor, resultado.Mensaje);
        Assert.DoesNotContain("detalle interno", resultado.Mensaje, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Login con respuesta inesperada no expone el JSON técnico")]
    public async Task LoginConRespuestaInesperadaDevuelveErrorGenerico()
    {
        var cliente = CrearCliente(new ManejadorFalso(HttpStatusCode.OK, "<html>error</html>"));

        var resultado = await cliente.IniciarSesionAsync("admin", "Admin123!");

        Assert.Equal(EstadoOperacion.ErrorServidor, resultado.Estado);
        Assert.Equal(MensajesAutenticacion.RespuestaInesperada, resultado.Mensaje);
        Assert.DoesNotContain("<html>", resultado.Mensaje, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "API caída: se informa sin exponer el detalle técnico")]
    public async Task ApiCaidaDevuelveServidorNoDisponible()
    {
        var excepcion = new HttpRequestException("No se pudo conectar al host remoto.");
        var cliente = CrearCliente(new ManejadorFalso(HttpStatusCode.OK, string.Empty, excepcion));

        var resultado = await cliente.IniciarSesionAsync("admin", "Admin123!");

        Assert.Equal(EstadoOperacion.ServidorNoDisponible, resultado.Estado);
        Assert.Equal(MensajesAutenticacion.ServidorNoDisponible, resultado.Mensaje);
        Assert.DoesNotContain("host remoto", resultado.Mensaje, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Tiempo de espera agotado: mensaje específico")]
    public async Task TiempoAgotadoDevuelveMensajeEspecifico()
    {
        var excepcion = new TaskCanceledException("Se agotó el tiempo de espera.", new TimeoutException());
        var cliente = CrearCliente(new ManejadorFalso(HttpStatusCode.OK, string.Empty, excepcion));

        var resultado = await cliente.IniciarSesionAsync("admin", "Admin123!");

        Assert.Equal(EstadoOperacion.TiempoAgotado, resultado.Estado);
        Assert.Equal(MensajesAutenticacion.TiempoAgotado, resultado.Mensaje);
    }

    [Fact(DisplayName = "Cancelación solicitada por la aplicación: estado Cancelado")]
    public async Task CancelacionSolicitadaDevuelveEstadoCancelado()
    {
        var cliente = CrearCliente(new ManejadorFalso(HttpStatusCode.OK, RespuestaLogin));
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();

        var resultado = await cliente.IniciarSesionAsync("admin", "Admin123!", cancelacion.Token);

        Assert.Equal(EstadoOperacion.Cancelado, resultado.Estado);
    }

    [Fact(DisplayName = "GET /me envía el token como Bearer")]
    public async Task UsuarioActualEnviaElTokenComoBearer()
    {
        var manejador = new ManejadorFalso(HttpStatusCode.OK, RespuestaUsuario);
        var cliente = CrearCliente(manejador);

        var resultado = await cliente.ObtenerUsuarioActualAsync(Token);

        Assert.True(resultado.EsExitoso);
        Assert.Equal(HttpMethod.Get, manejador.Metodo);
        Assert.Equal("/api/autenticacion/me", manejador.Ruta);
        Assert.Equal($"Bearer {Token}", manejador.Autorizacion);
        Assert.Equal("consultor", resultado.Datos!.Login);
        Assert.Equal(new[] { "Consultor" }, resultado.Datos.Roles);
    }

    [Fact(DisplayName = "ObtenerMeAsync toma el token de UserSession")]
    public async Task ObtenerMeUsaTokenDeSesion()
    {
        var manejador = new ManejadorFalso(HttpStatusCode.OK, RespuestaUsuario);
        using var http = new HttpClient(manejador) { BaseAddress = new Uri("http://localhost:5080/") };
        var cliente = new AuthenticationApiClient(http, CrearSesionAutenticada());

        var resultado = await cliente.ObtenerMeAsync();

        Assert.True(resultado.EsExitoso);
        Assert.Equal("/api/autenticacion/me", manejador.Ruta);
        Assert.Equal($"Bearer {Token}", manejador.Autorizacion);
    }

    [Fact(DisplayName = "GET /me con token vencido invalida la sesión y avisa una vez")]
    public async Task UsuarioActualCon401InvalidaLaSesion()
    {
        var cliente = CrearCliente(new ManejadorFalso(HttpStatusCode.Unauthorized, "{}"));
        var avisos = 0;
        cliente.SesionExpirada += (_, _) => avisos++;

        var resultado = await cliente.ObtenerUsuarioActualAsync(Token);

        Assert.Equal(EstadoOperacion.SesionExpirada, resultado.Estado);
        Assert.Equal(MensajesAutenticacion.SesionExpirada, resultado.Mensaje);
        Assert.Equal(1, avisos);
    }

    [Fact(DisplayName = "GET /me sin token no consulta la API")]
    public async Task UsuarioActualSinTokenNoConsultaLaApi()
    {
        var manejador = new ManejadorFalso(HttpStatusCode.OK, RespuestaUsuario);
        var cliente = CrearCliente(manejador);

        var resultado = await cliente.ObtenerUsuarioActualAsync(null);

        Assert.Equal(EstadoOperacion.SesionExpirada, resultado.Estado);
        Assert.Null(manejador.Ruta);
    }

    [Fact(DisplayName = "POST /cerrar: confirma el cierre con el token Bearer")]
    public async Task CerrarSesionConfirmaConTokenBearer()
    {
        var manejador = new ManejadorFalso(HttpStatusCode.NoContent, string.Empty);
        var cliente = CrearCliente(manejador);

        var resultado = await cliente.CerrarSesionAsync(Token);

        Assert.True(resultado.EsExitoso);
        Assert.True(resultado.Datos);
        Assert.Equal(HttpMethod.Post, manejador.Metodo);
        Assert.Equal("/api/autenticacion/cerrar", manejador.Ruta);
        Assert.Equal($"Bearer {Token}", manejador.Autorizacion);
    }

    [Fact(DisplayName = "CerrarSesionAsync toma el token de UserSession")]
    public async Task CerrarSesionUsaTokenDeSesion()
    {
        var manejador = new ManejadorFalso(HttpStatusCode.NoContent, string.Empty);
        using var http = new HttpClient(manejador) { BaseAddress = new Uri("http://localhost:5080/") };
        var cliente = new AuthenticationApiClient(http, CrearSesionAutenticada());

        var resultado = await cliente.CerrarSesionAsync();

        Assert.True(resultado.EsExitoso);
        Assert.Equal("/api/autenticacion/cerrar", manejador.Ruta);
        Assert.Equal($"Bearer {Token}", manejador.Autorizacion);
    }

    [Fact(DisplayName = "POST /cerrar sin red: informa el problema sin lanzar excepción")]
    public async Task CerrarSesionSinRedNoLanzaExcepcion()
    {
        var excepcion = new HttpRequestException("La conexión fue rechazada.");
        var cliente = CrearCliente(new ManejadorFalso(HttpStatusCode.OK, string.Empty, excepcion));

        var resultado = await cliente.CerrarSesionAsync(Token);

        Assert.False(resultado.EsExitoso);
        Assert.Equal(EstadoOperacion.ServidorNoDisponible, resultado.Estado);
    }

    [Fact(DisplayName = "POST /cerrar con 401 no vuelve a avisar del vencimiento")]
    public async Task CerrarSesionCon401NoAvisaDelVencimiento()
    {
        var cliente = CrearCliente(new ManejadorFalso(HttpStatusCode.Unauthorized, "{}"));
        var avisos = 0;
        cliente.SesionExpirada += (_, _) => avisos++;

        var resultado = await cliente.CerrarSesionAsync(Token);

        Assert.Equal(EstadoOperacion.SesionExpirada, resultado.Estado);
        Assert.Equal(0, avisos);
    }

    [Theory(DisplayName = "La cabecera Authorization agrega el prefijo Bearer una sola vez")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("Bearer ", null)]
    [InlineData("Bearer    ", null)]
    [InlineData("eyJx", "Bearer eyJx")]
    [InlineData("  eyJx  ", "Bearer eyJx")]
    [InlineData("Bearer eyJx", "Bearer eyJx")]
    [InlineData("bearer eyJx", "Bearer eyJx")]
    [InlineData("BEARER eyJx", "Bearer eyJx")]
    public void LaCabeceraBearerNoSeDuplica(string? token, string? esperado)
    {
        var autorizacion = AuthenticationApiClient.CrearAutorizacion(token);

        if (esperado is null)
        {
            Assert.Null(autorizacion);
            return;
        }

        Assert.NotNull(autorizacion);
        Assert.Equal(esperado, autorizacion!.ToString());
        Assert.Equal("Bearer", autorizacion.Scheme);
    }

    /// <summary>Manejador HTTP falso que devuelve la respuesta indicada y registra la petición.</summary>
    private sealed class ManejadorFalso : HttpMessageHandler
    {
        private readonly HttpStatusCode _codigo;
        private readonly string _contenido;
        private readonly Exception? _excepcion;

        public ManejadorFalso(HttpStatusCode codigo, string contenido, Exception? excepcion = null)
        {
            _codigo = codigo;
            _contenido = contenido;
            _excepcion = excepcion;
        }

        public HttpMethod? Metodo { get; private set; }

        public string? Ruta { get; private set; }

        public string? Cuerpo { get; private set; }

        public string? Autorizacion { get; private set; }

        public string? TipoContenido { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage solicitud,
            CancellationToken cancellationToken)
        {
            Metodo = solicitud.Method;
            Ruta = solicitud.RequestUri?.AbsolutePath;
            Autorizacion = solicitud.Headers.Authorization?.ToString();
            TipoContenido = solicitud.Content?.Headers.ContentType?.MediaType;

            if (solicitud.Content is not null)
            {
                Cuerpo = await solicitud.Content.ReadAsStringAsync(cancellationToken);
            }

            // Un manejador real interrumpe el envío cuando se cancela la operación.
            cancellationToken.ThrowIfCancellationRequested();

            if (_excepcion is not null)
            {
                throw _excepcion;
            }

            return new HttpResponseMessage(_codigo)
            {
                Content = new StringContent(_contenido, Encoding.UTF8, "application/json")
            };
        }
    }
}
