namespace Notebook.Data.Entities;

public class PrincipalClearanceEntity
{
    public byte[] AuthorId { get; set; } = null!;
    public Guid OrganizationId { get; set; }
    public string MaxLevel { get; set; } = "INTERNAL";
    public List<string> Compartments { get; set; } = [];
    public DateTimeOffset Granted { get; set; }
    public byte[]? GrantedBy { get; set; }
}
