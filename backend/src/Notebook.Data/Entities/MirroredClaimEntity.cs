using System.Text.Json;

namespace Notebook.Data.Entities;

public class MirroredClaimEntity
{
    public Guid Id { get; set; }
    public Guid SubscriptionId { get; set; }
    public Guid SourceEntryId { get; set; }
    public Guid NotebookId { get; set; }
    public JsonDocument Claims { get; set; } = null!;
    public string? Topic { get; set; }
    public double[]? Embedding { get; set; }
    public long SourceSequence { get; set; }
    public bool Tombstoned { get; set; }
    public DateTimeOffset MirroredAt { get; set; }
}
