namespace VisorDatosSIG.Application.DTOs;

public sealed class MigrationResult
{
    public required ShapefileLayer Layer { get; init; }
    public required MigrationMode Mode { get; init; }
    public required string DestinationTable { get; init; }
    public required string Status { get; init; }
    public required int SourceRecords { get; init; }
    public required int ProcessedRecords { get; init; }
    public required int InsertedRecords { get; init; }
    public required int OmittedRecords { get; init; }
    public required int FailedRecords { get; init; }
    public required TimeSpan Duration { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public bool Succeeded => string.Equals(Status, "EXITO", StringComparison.OrdinalIgnoreCase);
}
