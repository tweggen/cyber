namespace Notebook.Data.Entities;

public class EntryReviewEntity
{
    public Guid Id { get; set; }
    public Guid NotebookId { get; set; }
    public Guid EntryId { get; set; }
    public byte[] Submitter { get; set; } = null!;
    public string Status { get; set; } = "pending";
    public byte[]? Reviewer { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset Created { get; set; }
}
