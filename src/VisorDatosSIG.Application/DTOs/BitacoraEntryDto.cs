using VisorDatosSIG.Application.Common;

namespace VisorDatosSIG.Application.DTOs;

/// <summary>
/// Representa una entrada para el registro de auditoría y bitácora de operaciones del migrador.
/// Compatible con la tabla dbo.Bitacora de SQL Server.
/// </summary>
public sealed record BitacoraEntryDto
{
    public int? IdUsuario { get; init; } = 1;
    public DateTime FechaHora { get; init; } = DateTime.UtcNow;
    public string Modulo { get; init; } = ModulosSistema.MigradorDeDatosGeograficos;
    public string Accion { get; init; } = string.Empty;
    public string Entidad { get; init; } = string.Empty;
    public long? IdEntidad { get; init; }
    public string Resultado { get; init; } = "EXITO";
    public string Detalle { get; init; } = string.Empty;
    public string? IP { get; init; }
}
