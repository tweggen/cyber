namespace Notebook.Data.Entities;

public class OrganizationMemberEntity
{
    public Guid OrganizationId { get; set; }
    public byte[] AuthorId { get; set; } = null!;
    public string Role { get; set; } = null!;
    public DateTimeOffset Joined { get; set; }
}
