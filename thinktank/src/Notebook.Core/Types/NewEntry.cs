namespace Notebook.Core.Types;

/// <summary>
/// Input DTO for creating a new entry. Content is a string
/// (UTF-8 encoded to bytes by the repository).
/// </summary>
public sealed record NewEntry
{
    public required string Content { get; init; }
    public string ContentType { get; init; } = "text/plain";
    public string? Topic { get; init; }
    public List<Guid> References { get; init; } = [];
    public Guid? FragmentOf { get; init; }
    public int? FragmentIndex { get; init; }
    public string? OriginalContentType { get; init; }
}
