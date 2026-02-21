namespace Notebook.Data.Entities;

public class OrganizationEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public DateTimeOffset Created { get; set; }
}
