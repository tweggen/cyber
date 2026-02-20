namespace Notebook.Data.Entities;

public class GroupMembershipEntity
{
    public byte[] AuthorId { get; set; } = null!;
    public Guid GroupId { get; set; }
    public string Role { get; set; } = "member";
    public DateTimeOffset Granted { get; set; }
    public byte[]? GrantedBy { get; set; }
}
