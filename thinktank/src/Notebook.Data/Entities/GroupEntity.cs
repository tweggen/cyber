namespace Notebook.Data.Entities;

public class GroupEntity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = null!;
    public DateTimeOffset Created { get; set; }
}
