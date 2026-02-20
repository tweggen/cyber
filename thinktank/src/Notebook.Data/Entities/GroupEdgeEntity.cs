namespace Notebook.Data.Entities;

public class GroupEdgeEntity
{
    public Guid ParentGroupId { get; set; }
    public Guid ChildGroupId { get; set; }
    public DateTimeOffset Created { get; set; }
}
