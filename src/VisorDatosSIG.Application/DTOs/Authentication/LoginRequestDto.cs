using System.ComponentModel.DataAnnotations;

namespace VisorDatosSIG.Application.DTOs.Authentication;

/// <summary>
/// Datos de la solicitud de inicio de sesión.
/// </summary>
public sealed class LoginRequestDto
{
    /// <summary>Login del usuario.</summary>
    [Required(ErrorMessage = "El login es obligatorio.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "El login debe tener entre 1 y 100 caracteres.")]
    public string Login { get; set; } = string.Empty;

    /// <summary>Contraseña en texto plano recibida por HTTPS (nunca se almacena ni se registra).</summary>
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "La contraseña debe tener entre 1 y 200 caracteres.")]
    public string Password { get; set; } = string.Empty;
}
