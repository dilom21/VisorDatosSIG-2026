namespace VisorDatosSIG.Domain.Entities.Operations;

/// <summary>
/// Representa a un empleado registrado en el sistema que participa en actividades operativas,
/// seguimiento de servicios, asignación de rutas y bajas parciales (CU04).
/// </summary>
public sealed class Empleado
{
    public int IdEmpleado { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string DocumentoIdentidad { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string Cargo { get; set; } = string.Empty;
    public string Area { get; set; } = "Operaciones";
    public string Disponibilidad { get; set; } = "Disponible";
    public bool Activo { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public DateTime? FechaModificacion { get; set; }
    public string? Observaciones { get; set; }

    public string NombreCompleto => $"{Nombres} {Apellidos}".Trim();
}
