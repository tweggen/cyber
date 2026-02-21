namespace Cyber.Client.Pipeline;

public enum IngestionStage
{
    Detecting,
    Filtering,
    Uploading,
    Completed,
    Failed,
    Skipped
}

public sealed record IngestionProgress
{
    public required string FileName { get; init; }
    public required IngestionStage Stage { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }
}
