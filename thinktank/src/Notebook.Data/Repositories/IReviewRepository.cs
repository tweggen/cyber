using Notebook.Data.Entities;

namespace Notebook.Data.Repositories;

public interface IReviewRepository
{
    Task<EntryReviewEntity> CreateAsync(EntryReviewEntity review, CancellationToken ct);
    Task<EntryReviewEntity?> GetAsync(Guid reviewId, CancellationToken ct);
    Task<List<EntryReviewEntity>> ListByNotebookAsync(Guid notebookId, string? statusFilter, CancellationToken ct);
    Task<int> CountPendingAsync(Guid notebookId, CancellationToken ct);
    Task ApproveAsync(Guid reviewId, byte[] reviewerId, CancellationToken ct);
    Task RejectAsync(Guid reviewId, byte[] reviewerId, CancellationToken ct);
    Task SetEntryReviewStatusAsync(Guid entryId, string status, CancellationToken ct);
}
