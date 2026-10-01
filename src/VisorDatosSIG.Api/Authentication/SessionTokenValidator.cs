using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Authentication;

namespace VisorDatosSIG.Api.Authentication;

// Respeta desactivaciones y cambios de permisos en sesiones ya abiertas.
public static class SessionTokenValidator
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var sub = context.Principal?.FindFirst("sub")?.Value;
        if (!int.TryParse(sub, out var id) || id <= 0)
        {
            context.Fail("Usuario inválido.");
            return;
        }
        var repository = context.HttpContext.RequestServices.GetRequiredService<IUsuarioRepository>();
        var usuario = await repository.ObtenerPorIdAsync(id, context.HttpContext.RequestAborted);
        if (usuario is null || !usuario.Activo)
        {
            context.Fail("Usuario inactivo o inexistente.");
            return;
        }
        var identity = (ClaimsIdentity)context.Principal!.Identity!;
        foreach (var claim in identity.FindAll(JwtTokenService.ClaimRol).ToArray())
            identity.RemoveClaim(claim);
        foreach (var rol in usuario.Roles)
            identity.AddClaim(new Claim(JwtTokenService.ClaimRol, rol));
        foreach (var claim in identity.FindAll(JwtTokenService.ClaimNombre).ToArray())
            identity.RemoveClaim(claim);
        identity.AddClaim(new Claim(JwtTokenService.ClaimNombre, usuario.Nombre));
    }
}
