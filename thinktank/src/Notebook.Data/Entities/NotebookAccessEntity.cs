namespace Notebook.Data.Entities;

public class NotebookAccessEntity
{
    public Guid NotebookId { get; set; }
    public byte[] AuthorId { get; set; } = null!;
    public bool Read { get; set; }
    public bool Write { get; set; }
    public DateTimeOffset Granted { get; set; }
}
