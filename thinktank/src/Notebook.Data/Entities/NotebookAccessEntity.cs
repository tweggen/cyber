namespace Notebook.Data.Entities;

public class NotebookAccessEntity
{
    public Guid NotebookId { get; set; }
    public byte[] AuthorId { get; set; } = null!;
    public string Tier { get; set; } = "read_write";
    public DateTimeOffset Granted { get; set; }
}
