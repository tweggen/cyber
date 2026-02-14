namespace Notebook.Data.Entities;

public class NotebookEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public byte[] OwnerId { get; set; } = null!;
    public DateTimeOffset Created { get; set; }
    public long CurrentSequence { get; set; }
}
