using System.Text.Json;

namespace Notebook.Data.Entities;

public class JobEntity
{
    public Guid Id { get; set; }
    public Guid NotebookId { get; set; }
    public string JobType { get; set; } = null!;
    public string Status { get; set; } = "pending";
    public JsonDocument Payload { get; set; } = null!;
    public JsonDocument? Result { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? ClaimedAt { get; set; }
    public string? ClaimedBy { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int TimeoutSeconds { get; set; } = 120;
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
    public int Priority { get; set; }
}
