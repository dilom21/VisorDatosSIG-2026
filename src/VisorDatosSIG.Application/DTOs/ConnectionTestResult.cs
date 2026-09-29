namespace VisorDatosSIG.Application.DTOs;

public sealed class ConnectionTestResult
{
    public required bool CanConnect { get; init; }
    public required bool DatabaseExists { get; init; }
    public required bool RequiredTablesExist { get; init; }
    public required bool HasWriteAccess { get; init; }
    public required string Message { get; init; }
    public IReadOnlyList<string> MissingTables { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public bool IsReady => CanConnect && DatabaseExists && RequiredTablesExist && HasWriteAccess;
}
