using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Migrador.Services;
using VisorDatosSIG.Migrador.Session;

namespace VisorDatosSIG.Migrador.UnitTests;

/// <summary>
/// Pruebas de la sesión en memoria del Migrador: datos guardados, token y limpieza completa.
/// </summary>
public sealed class UserSessionTests
{
    private const string Token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.carga.firma";

    private static AuthenticatedUserDto CrearUsuario(
        int idUsuario = 1,
        string login = "admin",
        string nombre = "Administrador",
        params string[] roles) =>
        new()
        {
            IdUsuario = idUsuario,
            Login = login,
            Nombre = nombre,
            Roles = roles.Length > 0 ? roles : ["Administrador"]
        };

    [Fact(DisplayName = "Establecer guarda token, usuario, roles y vencimiento")]
    public void EstablecerGuardaTodosLosDatos()
    {
        var sesion = new UserSession();

        sesion.Establecer(Token, CrearUsuario(7, "admin", "Administrador"), 1800);

        Assert.True(sesion.IsAuthenticated);
        Assert.Equal(Token, sesion.AccessToken);
        Assert.Equal(7, sesion.IdUsuario);
        Assert.Equal("admin", sesion.Login);
        Assert.Equal("Administrador", sesion.Nombre);
        Assert.Equal(new[] { "Administrador" }, sesion.Roles);
        Assert.Equal("Administrador", sesion.RolPrincipal);
        Assert.Equal("Administrador", sesion.NombreParaMostrar);
        Assert.True(sesion.ExpiraEn > DateTimeOffset.Now);
    }

    [Fact(DisplayName = "El token se guarda sin el prefijo Bearer")]
    public void ElTokenSeGuardaSinPrefijoBearer()
    {
        var sesion = new UserSession();

        sesion.Establecer("  Bearer   " + Token + "  ", CrearUsuario(), 1800);

        Assert.Equal(Token, sesion.AccessToken);
        Assert.DoesNotContain("Bearer", sesion.AccessToken!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Una sesión nueva no está autenticada")]
    public void UnaSesionNuevaNoEstaAutenticada()
    {
        var sesion = new UserSession();

        Assert.False(sesion.IsAuthenticated);
        Assert.Null(sesion.AccessToken);
        Assert.Null(sesion.IdUsuario);
        Assert.Null(sesion.ExpiraEn);
        Assert.Empty(sesion.Login);
        Assert.Empty(sesion.Nombre);
        Assert.Empty(sesion.Roles);
        Assert.Equal("-", sesion.RolesTexto);
    }

    [Fact(DisplayName = "Clear elimina por completo la sesión")]
    public void ClearEliminaPorCompletoLaSesion()
    {
        var sesion = new UserSession();
        sesion.Establecer(Token, CrearUsuario(), 1800);

        sesion.Clear();

        Assert.False(sesion.IsAuthenticated);
        Assert.Null(sesion.AccessToken);
        Assert.Null(sesion.IdUsuario);
        Assert.Null(sesion.ExpiraEn);
        Assert.Empty(sesion.Login);
        Assert.Empty(sesion.Nombre);
        Assert.Empty(sesion.Roles);
        Assert.Equal("-", sesion.RolPrincipal);
    }

    [Fact(DisplayName = "Clear avisa una sola vez del cambio de estado")]
    public void ClearAvisaUnaSolaVez()
    {
        var sesion = new UserSession();
        var avisos = 0;
        sesion.EstadoCambiado += (_, _) => avisos++;

        sesion.Clear();

        Assert.Equal(0, avisos);

        sesion.Establecer(Token, CrearUsuario(), 1800);
        Assert.Equal(1, avisos);

        sesion.Clear();
        Assert.Equal(2, avisos);

        sesion.Clear();
        Assert.Equal(2, avisos);
    }

    [Fact(DisplayName = "El token no se acepta vacío")]
    public void ElTokenNoSeAceptaVacio()
    {
        var sesion = new UserSession();

        Assert.Throws<ArgumentException>(() => sesion.Establecer("   ", CrearUsuario(), 1800));
        Assert.Throws<ArgumentException>(() => sesion.Establecer("Bearer   ", CrearUsuario(), 1800));
        Assert.Throws<ArgumentNullException>(() => sesion.Establecer(Token, null!, 1800));
        Assert.False(sesion.IsAuthenticated);
    }

    [Fact(DisplayName = "ActualizarUsuario refresca los datos sin cambiar el token")]
    public void ActualizarUsuarioRefrescaLosDatosSinCambiarElToken()
    {
        var sesion = new UserSession();
        sesion.Establecer(Token, CrearUsuario(1, "admin", "Administrador"), 1800);

        sesion.ActualizarUsuario(CrearUsuario(1, "admin", "Ana Pérez", "Administrador", "Consultor"));

        Assert.Equal(Token, sesion.AccessToken);
        Assert.Equal("Ana Pérez", sesion.Nombre);
        Assert.Equal(new[] { "Administrador", "Consultor" }, sesion.Roles);
        Assert.Equal("Administrador, Consultor", sesion.RolesTexto);
    }

    [Fact(DisplayName = "Sin sesión no se actualizan datos de usuario")]
    public void SinSesionNoSeActualizanDatosDeUsuario()
    {
        var sesion = new UserSession();

        sesion.ActualizarUsuario(CrearUsuario());

        Assert.False(sesion.IsAuthenticated);
        Assert.Empty(sesion.Login);
    }

    [Theory(DisplayName = "Las iniciales del avatar se calculan a partir del nombre o del login")]
    [InlineData("Administrador", "admin", "A")]
    [InlineData("Ana Pérez", "aperez", "AP")]
    [InlineData("  ", "consultor", "C")]
    [InlineData(null, null, "?")]
    public void LasInicialesDelAvatarSeCalculanCorrectamente(string? nombre, string? login, string esperado)
    {
        Assert.Equal(esperado, UserSession.CalcularIniciales(nombre, login));
    }

    [Fact(DisplayName = "La sesión completa el prefijo Bearer con una sola aparición")]
    public void LaSesionCompletaElPrefijoBearerUnaSolaVez()
    {
        var sesion = new UserSession();
        sesion.Establecer(Token, CrearUsuario(), 1800);

        var autorizacion = AuthenticationApiClient.CrearAutorizacion(sesion.AccessToken);

        Assert.NotNull(autorizacion);
        Assert.Equal("Bearer", autorizacion!.Scheme);
        Assert.Equal(Token, autorizacion.Parameter);
        Assert.Equal($"Bearer {Token}", autorizacion.ToString());
    }
}
