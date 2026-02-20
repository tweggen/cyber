using System.Text.Json;

namespace Notebook.Core.Types;

public sealed record AuditEvent
{
    public Guid? NotebookId { get; init; }
    public byte[]? AuthorId { get; init; }
    public required string Action { get; init; }
    public string? TargetType { get; init; }
    public string? TargetId { get; init; }
    public JsonElement? Detail { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}
