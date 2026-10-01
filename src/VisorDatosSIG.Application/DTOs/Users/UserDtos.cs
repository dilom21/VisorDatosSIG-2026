using System.ComponentModel.DataAnnotations;

namespace VisorDatosSIG.Application.DTOs.Users;

public sealed record ChangePasswordRequestDto(
    [Required, StringLength(200)] string PasswordActual,
    [Required, StringLength(200, MinimumLength = 8)] string PasswordNueva,
    [Required] string ConfirmarPassword) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PasswordNueva != ConfirmarPassword)
            yield return new ValidationResult("La nueva contraseña y la confirmación no coinciden.", [nameof(ConfirmarPassword)]);
    }
}

public sealed record CreateUserRequestDto(
    [Required, StringLength(100)] string Login,
    [Required, StringLength(240)] string Nombre,
    [Required, StringLength(200, MinimumLength = 8)] string PasswordInicial,
    [Required, MinLength(1)] List<string> Roles);

public sealed record UpdateUserRequestDto(
    [Required, StringLength(240)] string Nombre,
    [Required, MinLength(1)] List<string> Roles);

// Las respuestas nunca incluyen credenciales, hash ni salt.
public sealed record UserResponseDto(
    int IdUsuario, string Login, string Nombre, bool Activo,
    DateTime FechaRegistro, List<string> Roles);

public sealed record ChangeStatusRequestDto(bool Activo);

public sealed record ResetPasswordRequestDto(
    [Required, StringLength(200, MinimumLength = 8)] string NuevaPassword);
