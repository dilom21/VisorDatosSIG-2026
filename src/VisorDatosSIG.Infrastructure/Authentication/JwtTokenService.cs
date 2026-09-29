using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Infrastructure.Authentication;

/// <summary>
/// Generación de tokens JWT firmados con HMAC-SHA256.
/// </summary>
/// <remarks>
/// El token incluye <c>sub</c> (IdUsuario), <c>login</c>, <c>name</c>, un <c>role</c> por cada
/// rol activo, <c>jti</c>, <c>iat</c> y <c>exp</c>. No incluye datos sensibles.
/// </remarks>
public sealed class JwtTokenService : ITokenService
{
    /// <summary>Nombre del claim con el login del usuario.</summary>
    public const string ClaimLogin = "login";

    /// <summary>Nombre del claim con el nombre del usuario.</summary>
    public const string ClaimNombre = "name";

    /// <summary>Nombre del claim con cada rol del usuario.</summary>
    public const string ClaimRol = "role";

    private readonly JwtSettings _settings;

    /// <summary>
    /// Inicializa el servicio con la configuración del token.
    /// </summary>
    /// <param name="settings">Configuración de JWT.</param>
    public JwtTokenService(JwtSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <inheritdoc />
    public TokenGeneradoDto GenerarToken(AuthenticatedUserDto usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var emitidoEn = DateTime.UtcNow;
        var expiraEn = emitidoEn.AddMinutes(_settings.ExpirationMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.IdUsuario.ToString(CultureInfo.InvariantCulture)),
            new(ClaimLogin, usuario.Login),
            new(ClaimNombre, usuario.Nombre),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(
                JwtRegisteredClaimNames.Iat,
                EpochTime.GetIntDate(emitidoEn).ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        };

        claims.AddRange(usuario.Roles.Select(rol => new Claim(ClaimRol, rol)));

        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: emitidoEn,
            expires: expiraEn,
            signingCredentials: credenciales);

        return new TokenGeneradoDto
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiraEnSegundos = (int)Math.Max(1, Math.Round((expiraEn - emitidoEn).TotalSeconds))
        };
    }
}
