using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Application.DTOs.Users;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Application.DTOs.Authentication;

namespace VisorDatosSIG.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PerfilController : ControllerBase
{
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly IBitacoraRepository _bitacoraRepo;

    public PerfilController(IUsuarioRepository usuarioRepo, IBitacoraRepository bitacoraRepo)
    {
        _usuarioRepo = usuarioRepo;
        _bitacoraRepo = bitacoraRepo;
    }

    [HttpPut("cambiar-password")]
    public async Task<IActionResult> CambiarPassword([FromBody] ChangePasswordRequestDto dto, CancellationToken ct)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!int.TryParse(sub, out var idUsuario) || idUsuario <= 0)
            return Unauthorized();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (string.IsNullOrEmpty(dto.PasswordActual))
            return BadRequest(new { mensaje = "La contraseña actual es obligatoria." });

        if (dto.PasswordActual == dto.PasswordNueva)
            return BadRequest(new { mensaje = "La nueva contraseña debe ser diferente a la actual." });

        if (dto.PasswordNueva != dto.ConfirmarPassword)
        {
            await RegistrarCambioPasswordAsync(idUsuario, BitacoraEventos.ResultadoFallido, "La nueva contraseña y la confirmación no coinciden.", ip, ct);
            return BadRequest(new { mensaje = "La nueva contraseña y la confirmación no coinciden." });
        }

        if (string.IsNullOrWhiteSpace(dto.PasswordNueva) || dto.PasswordNueva.Length < 8)
        {
            await RegistrarCambioPasswordAsync(idUsuario, BitacoraEventos.ResultadoFallido, "La contraseña no cumple la longitud mínima.", ip, ct);
            return BadRequest(new { mensaje = "La contraseña debe tener al menos 8 caracteres." });
        }

        var exito = await _usuarioRepo.CambiarPasswordAsync(idUsuario, dto.PasswordActual, dto.PasswordNueva, ct);

        if (!exito)
        {
            await RegistrarCambioPasswordAsync(idUsuario, BitacoraEventos.ResultadoFallido, "Contraseña actual incorrecta.", ip, ct);
            return BadRequest(new { mensaje = "La contraseña actual es incorrecta." });
        }

        await RegistrarCambioPasswordAsync(idUsuario, BitacoraEventos.ResultadoExitoso, "Contraseña actualizada exitosamente.", ip, ct);
        return Ok(new { mensaje = "Contraseña actualizada exitosamente." });
    }

    private Task RegistrarCambioPasswordAsync(int idUsuario, string resultado, string detalle, string? ip, CancellationToken ct)
    {
        return _bitacoraRepo.RegistrarAsync(new BitacoraRegistroDto
        {
            Modulo = BitacoraEventos.ModuloAutenticacion,
            Accion = "CAMBIO_PASSWORD",
            Resultado = resultado,
            Entidad = BitacoraEventos.EntidadUsuario,
            IdEntidad = idUsuario,
            IdUsuario = idUsuario,
            Detalle = detalle,
            Ip = ip
        }, ct);
    }
}
