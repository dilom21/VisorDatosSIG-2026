using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Infrastructure.Authentication;

namespace VisorDatosSIG.UnitTests;

/// <summary>
/// Pruebas de la generación de tokens JWT.
/// </summary>
public sealed class JwtTokenServiceTests
{
    private const string Clave = "clave-de-pruebas-para-jwt-visor-datos-sig-2026-32b";

    private static readonly JwtSettings Configuracion = new()
    {
        Issuer = "VisorDatosSIG.Api.Pruebas",
        Audience = "VisorDatosSIG.Clientes.Pruebas",
        Key = Clave,
        ExpirationMinutes = 30
    };

    private static AuthenticatedUserDto Usuario(params string[] roles) => new()
    {
        IdUsuario = 1,
        Login = "admin",
        Nombre = "Administrador",
        Roles = roles.Length == 0 ? ["Administrador"] : roles
    };

    [Fact(DisplayName = "JWT: contiene sub, login, name, role, jti, iat y exp")]
    public void TokenContieneLosClaimsRequeridos()
    {
        var servicio = new JwtTokenService(Configuracion);

        var token = servicio.GenerarToken(Usuario());

        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.AccessToken);

        Assert.Equal("1", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("admin", jwt.Claims.Single(c => c.Type == JwtTokenService.ClaimLogin).Value);
        Assert.Equal("Administrador", jwt.Claims.Single(c => c.Type == JwtTokenService.ClaimNombre).Value);
        Assert.Equal("Administrador", jwt.Claims.Single(c => c.Type == JwtTokenService.ClaimRol).Value);
        Assert.False(string.IsNullOrWhiteSpace(jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value));
        Assert.False(string.IsNullOrWhiteSpace(jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Iat).Value));
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Exp);

        Assert.Equal(Configuracion.Issuer, jwt.Issuer);
        Assert.Contains(Configuracion.Audience, jwt.Audiences);
    }

    [Fact(DisplayName = "JWT: incluye un claim role por cada rol del usuario")]
    public void TokenIncluyeTodosLosRoles()
    {
        var servicio = new JwtTokenService(Configuracion);

        var token = servicio.GenerarToken(Usuario("Administrador", "Consultor"));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.AccessToken);
        var roles = jwt.Claims.Where(c => c.Type == JwtTokenService.ClaimRol).Select(c => c.Value).ToArray();

        Assert.Equal(2, roles.Length);
        Assert.Contains("Administrador", roles);
        Assert.Contains("Consultor", roles);
    }

    [Fact(DisplayName = "JWT: la vigencia respeta ExpirationMinutes")]
    public void VigenciaRespetaLaConfiguracion()
    {
        var servicio = new JwtTokenService(Configuracion);

        var token = servicio.GenerarToken(Usuario());

        Assert.Equal(1800, token.ExpiraEnSegundos);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.AccessToken);
        var margen = jwt.ValidTo - jwt.ValidFrom;

        Assert.True(margen >= TimeSpan.FromMinutes(29) && margen <= TimeSpan.FromMinutes(31), $"Margen inesperado: {margen}");
    }

    [Fact(DisplayName = "JWT: el token se firma con HMAC-SHA256 y es validable con la clave configurada")]
    public async Task TokenEsValidableConLaClaveConfigurada()
    {
        var servicio = new JwtTokenService(Configuracion);
        var token = servicio.GenerarToken(Usuario());

        var parametros = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Configuracion.Issuer,
            ValidateAudience = true,
            ValidAudience = Configuracion.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuracion.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = await handler.ValidateTokenAsync(token.AccessToken, parametros);

        Assert.True(principal.IsValid);
        Assert.Equal("admin", principal.ClaimsIdentity.FindFirst(JwtTokenService.ClaimLogin)?.Value);
    }

    [Fact(DisplayName = "JWT: la validación falla con una clave distinta")]
    public async Task ValidacionFallaConOtraClave()
    {
        var servicio = new JwtTokenService(Configuracion);
        var token = servicio.GenerarToken(Usuario());

        var parametros = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Configuracion.Issuer,
            ValidateAudience = true,
            ValidAudience = Configuracion.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("otra-clave-distinta-de-pruebas-para-jwt-2026-32b")),
            ValidateLifetime = true
        };

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = await handler.ValidateTokenAsync(token.AccessToken, parametros);

        Assert.False(principal.IsValid);
    }

    [Theory(DisplayName = "JWT: configuración incompleta es detectada")]
    [InlineData("", "aud", Clave, 30)]
    [InlineData("iss", "", Clave, 30)]
    [InlineData("iss", "aud", "", 30)]
    [InlineData("iss", "aud", "corta", 30)]
    [InlineData("iss", "aud", Clave, 0)]
    public void ConfiguracionIncompletaEsDetectada(string issuer, string audience, string key, int minutos)
    {
        var configuracion = new JwtSettings
        {
            Issuer = issuer,
            Audience = audience,
            Key = key,
            ExpirationMinutes = minutos
        };

        Assert.NotNull(configuracion.Validar());
    }
}
