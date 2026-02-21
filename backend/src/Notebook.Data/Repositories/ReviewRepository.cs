using Microsoft.EntityFrameworkCore;
using Notebook.Data.Entities;

namespace Notebook.Data.Repositories;

public class ReviewRepository(NotebookDbContext db) : IReviewRepository
{
    public async Task<EntryReviewEntity> CreateAsync(EntryReviewEntity review, CancellationToken ct)
    {
        db.EntryReviews.Add(review);
        await db.SaveChangesAsync(ct);
        return review;
    }

    public async Task<EntryReviewEntity?> GetAsync(Guid reviewId, CancellationToken ct)
    {
        return await db.EntryReviews.FirstOrDefaultAsync(r => r.Id == reviewId, ct);
    }

    public async Task<List<EntryReviewEntity>> ListByNotebookAsync(
        Guid notebookId, string? statusFilter, CancellationToken ct)
    {
        var query = db.EntryReviews.Where(r => r.NotebookId == notebookId);

        if (statusFilter is not null)
            query = query.Where(r => r.Status == statusFilter);

        return await query
            .OrderByDescending(r => r.Created)
            .ToListAsync(ct);
    }

    public async Task<int> CountPendingAsync(Guid notebookId, CancellationToken ct)
    {
        return await db.EntryReviews
            .CountAsync(r => r.NotebookId == notebookId && r.Status == "pending", ct);
    }

    public async Task ApproveAsync(Guid reviewId, byte[] reviewerId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlAsync(
            $"UPDATE entry_reviews SET status = 'approved', reviewer = {reviewerId}, reviewed_at = NOW() WHERE id = {reviewId}",
            ct);
    }

    public async Task RejectAsync(Guid reviewId, byte[] reviewerId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlAsync(
            $"UPDATE entry_reviews SET status = 'rejected', reviewer = {reviewerId}, reviewed_at = NOW() WHERE id = {reviewId}",
            ct);
    }

    public async Task SetEntryReviewStatusAsync(Guid entryId, string status, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE entries SET review_status = {0} WHERE id = {1}",
            [status, entryId], ct);
    }
}
