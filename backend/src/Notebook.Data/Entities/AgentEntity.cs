namespace Notebook.Data.Entities;

public class AgentEntity
{
    public string Id { get; set; } = null!;
    public Guid OrganizationId { get; set; }
    public string MaxLevel { get; set; } = "INTERNAL";
    public List<string> Compartments { get; set; } = [];
    public string? Infrastructure { get; set; }
    public DateTimeOffset Registered { get; set; }
    public DateTimeOffset? LastSeen { get; set; }
}
