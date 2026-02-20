namespace Notebook.Data.Entities;

public class OrganizationEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public byte[] OwnerId { get; set; } = null!;
    public DateTimeOffset Created { get; set; }
}
