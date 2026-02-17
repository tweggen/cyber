namespace Cyber.Client.Pipeline;

public sealed record FileResult
{
    public required string FileName { get; init; }
    public required bool Success { get; init; }
    public string? Error { get; init; }
}

public sealed record IngestionResult
{
    public required int Succeeded { get; init; }
    public required int Failed { get; init; }
    public required int Skipped { get; init; }
    public required List<FileResult> Details { get; init; }
}
