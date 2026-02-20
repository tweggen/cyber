namespace Notebook.Data.Entities;

public class GroupMemberEntity
{
    public Guid GroupId { get; set; }
    public byte[] AuthorId { get; set; } = null!;
    public DateTimeOffset Joined { get; set; }
}
