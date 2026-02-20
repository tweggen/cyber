namespace Notebook.Data.Entities;

public class AuditLogEntity
{
    public Guid Id { get; set; }
    public byte[] Actor { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string Resource { get; set; } = null!;
    public string? Detail { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset Created { get; set; }
}
