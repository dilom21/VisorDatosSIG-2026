namespace VisorDatosSIG.Application.DTOs;

/// <summary>
/// Elemento de lectura del historial de auditoría y bitácora de SQL Server (RF-MIG-10, CU30).
/// </summary>
public sealed record BitacoraItemDto
{
    public long IdBitacora { get; init; }
    public int? IdUsuario { get; init; }
    public DateTime FechaHora { get; init; }
    public string Modulo { get; init; } = string.Empty;
    public string Accion { get; init; } = string.Empty;
    public string Entidad { get; init; } = string.Empty;
    public long? IdEntidad { get; init; }
    public string Resultado { get; init; } = string.Empty;
    public string Detalle { get; init; } = string.Empty;
    public string? IP { get; init; }
}
