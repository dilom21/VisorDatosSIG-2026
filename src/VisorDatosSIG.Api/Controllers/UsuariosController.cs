using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Application.DTOs.Users;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Application.DTOs.Authentication;

namespace VisorDatosSIG.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador")]
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly IBitacoraRepository _bitacoraRepo;

    public UsuariosController(IUsuarioRepository usuarioRepo, IBitacoraRepository bitacoraRepo)
    {
        _usuarioRepo = usuarioRepo;
        _bitacoraRepo = bitacoraRepo;
    }

    [HttpGet("roles")]
    public async Task<IActionResult> ObtenerRoles(CancellationToken ct)
    {
        var roles = await _usuarioRepo.ObtenerRolesAsync(ct);
        return Ok(roles);
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerTodos(CancellationToken ct)
    {
        var usuarios = await _usuarioRepo.ObtenerTodosAsync(ct);
        return Ok(usuarios);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObtenerPorId(int id, CancellationToken ct)
    {
        var usuario = await _usuarioRepo.ObtenerPorIdAsync(id, ct);
        if (usuario is null) return NotFound(new { mensaje = "Usuario no encontrado." });
        return Ok(usuario);
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CreateUserRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Login) || string.IsNullOrWhiteSpace(dto.Nombre) || string.IsNullOrWhiteSpace(dto.PasswordInicial))
            return BadRequest(new { mensaje = "Datos obligatorios incompletos." });

        if (dto.PasswordInicial.Length < 8)
            return BadRequest(new { mensaje = "La contraseña inicial debe tener al menos 8 caracteres." });

        var rolesDisponibles = await _usuarioRepo.ObtenerRolesAsync(ct);
        if (dto.Roles is null || dto.Roles.Count == 0 || dto.Roles.Any(rol => !rolesDisponibles.Contains(rol, StringComparer.OrdinalIgnoreCase)))
            return BadRequest(new { mensaje = "Seleccione al menos un rol disponible." });

        dto = dto with { Login = dto.Login.Trim(), Nombre = dto.Nombre.Trim(), Roles = dto.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList() };
        var nuevo = await _usuarioRepo.CrearUsuarioAsync(dto, ct);
        if (nuevo is null)
            return BadRequest(new { mensaje = "El login ya se encuentra en uso." });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _bitacoraRepo.RegistrarAsync(new BitacoraRegistroDto
        {
            Modulo = BitacoraEventos.ModuloAutenticacion,
            Accion = "ALTA_USUARIO",
            Resultado = BitacoraEventos.ResultadoExitoso,
            IdUsuario = ObtenerIdUsuarioActual(),
            Entidad = BitacoraEventos.EntidadUsuario,
            IdEntidad = nuevo.IdUsuario,
            Detalle = $"Usuario creado: {dto.Login}",
            Ip = ip
        }, ct);


        return CreatedAtAction(nameof(ObtenerPorId), new { id = nuevo.IdUsuario }, nuevo);
    }

    [HttpPatch("{id:int}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] ChangeStatusRequestDto dto, CancellationToken ct)
    {
        var exito = await _usuarioRepo.CambiarEstadoAsync(id, dto.Activo, ct);
        if (!exito) return NotFound(new { mensaje = "Usuario no encontrado." });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _bitacoraRepo.RegistrarAsync(new BitacoraRegistroDto
        {
            Modulo = BitacoraEventos.ModuloAutenticacion,
            Accion = "CAMBIO_ESTADO",
            Resultado = BitacoraEventos.ResultadoExitoso,
            IdUsuario = ObtenerIdUsuarioActual(),
            Entidad = BitacoraEventos.EntidadUsuario,
            IdEntidad = id,
            Detalle = $"Estado actualizado a {dto.Activo} para ID: {id}",
            Ip = ip
        }, ct);


        return Ok(new { mensaje = "Estado actualizado." });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] UpdateUserRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest(new { mensaje = "El nombre es obligatorio." });

        var rolesDisponibles = await _usuarioRepo.ObtenerRolesAsync(ct);
        if (dto.Roles is null || dto.Roles.Count == 0 || dto.Roles.Any(rol => !rolesDisponibles.Contains(rol, StringComparer.OrdinalIgnoreCase)))
            return BadRequest(new { mensaje = "Seleccione únicamente roles disponibles." });

        var actualizado = await _usuarioRepo.ActualizarUsuarioAsync(id, dto, ct);
        if (actualizado is null)
            return NotFound(new { mensaje = "Usuario no encontrado." });

        await RegistrarCambioAsync("ACTUALIZACION_USUARIO", id, $"Nombre y roles actualizados para ID: {id}.", ct);
        return Ok(actualizado);
    }

    [HttpPost("{id:int}/restablecer-password")]
    public async Task<IActionResult> RestablecerPassword(int id, [FromBody] ResetPasswordRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.NuevaPassword) || dto.NuevaPassword.Length < 8)
            return BadRequest(new { mensaje = "La nueva contraseña debe tener al menos 8 caracteres." });

        var actualizado = await _usuarioRepo.RestablecerPasswordAsync(id, dto.NuevaPassword, ct);
        if (!actualizado)
            return NotFound(new { mensaje = "Usuario no encontrado." });

        await RegistrarCambioAsync("RESTABLECIMIENTO_PASSWORD", id, $"Contraseña restablecida para ID: {id}.", ct);
        return NoContent();
    }

    private Task RegistrarCambioAsync(string accion, int idUsuarioAfectado, string detalle, CancellationToken ct)
    {
        return _bitacoraRepo.RegistrarAsync(new BitacoraRegistroDto
        {
            Modulo = BitacoraEventos.ModuloAutenticacion,
            Accion = accion,
            Resultado = BitacoraEventos.ResultadoExitoso,
            IdUsuario = ObtenerIdUsuarioActual(),
            Entidad = BitacoraEventos.EntidadUsuario,
            IdEntidad = idUsuarioAfectado,
            Detalle = detalle,
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString()
        }, ct);
    }

    private int? ObtenerIdUsuarioActual()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(id, out var resultado) ? resultado : null;
    }
}
