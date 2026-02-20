namespace Notebook.Data.Entities;

public class MirroredEntryEntity
{
    public Guid Id { get; set; }
    public Guid SubscriptionId { get; set; }
    public Guid SourceEntryId { get; set; }
    public Guid NotebookId { get; set; }
    public byte[] Content { get; set; } = null!;
    public string ContentType { get; set; } = "text/plain";
    public string? Topic { get; set; }
    public long SourceSequence { get; set; }
    public bool Tombstoned { get; set; }
    public DateTimeOffset MirroredAt { get; set; }
}
