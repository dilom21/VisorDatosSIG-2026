namespace VisorDatosSIG.Application.DTOs;

public sealed class MigrationOptions
{
    public required string ShapefilePath { get; init; }
    public required ShapefileLayer Layer { get; init; }
    public required MigrationMode Mode { get; init; }
    public int BatchSize { get; init; } = 500;
}
