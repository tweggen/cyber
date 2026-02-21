using Microsoft.EntityFrameworkCore;
using Notebook.Data.Entities;

namespace Notebook.Data.Repositories;

public class NotebookRepository(NotebookDbContext db) : INotebookRepository
{
    public async Task<NotebookEntity?> GetByIdAsync(Guid notebookId, CancellationToken ct)
    {
        return await db.Notebooks.FirstOrDefaultAsync(n => n.Id == notebookId, ct);
    }

    public async Task<List<NotebookEntity>> ListNotebooksAsync(byte[] authorId, CancellationToken ct)
    {
        // Notebooks where the user is owner OR has been granted access
        return await db.Notebooks
            .Where(n => n.OwnerId == authorId
                || db.NotebookAccess.Any(a => a.NotebookId == n.Id && a.AuthorId == authorId))
            .OrderByDescending(n => n.Created)
            .ToListAsync(ct);
    }

    public async Task<NotebookEntity> CreateNotebookAsync(string name, byte[] ownerId, CancellationToken ct,
        string classification = "INTERNAL", List<string>? compartments = null)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Ensure the author exists in the authors table (required by notebooks.owner_id FK).
        // Uses the author_id as a synthetic public_key placeholder when no real key is available.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO authors (id, public_key) VALUES ({0}, {1}) ON CONFLICT DO NOTHING",
            [ownerId, ownerId], ct);

        var notebook = new NotebookEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            OwnerId = ownerId,
            Created = DateTimeOffset.UtcNow,
            CurrentSequence = 0,
            Classification = classification,
            Compartments = compartments ?? [],
        };

        db.Notebooks.Add(notebook);

        db.NotebookAccess.Add(new NotebookAccessEntity
        {
            NotebookId = notebook.Id,
            AuthorId = ownerId,
            Tier = "admin",
            Granted = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return notebook;
    }

    public async Task<bool> DeleteNotebookAsync(Guid notebookId, byte[] requestingAuthorId, CancellationToken ct)
    {
        var notebook = await db.Notebooks.FirstOrDefaultAsync(
            n => n.Id == notebookId && n.OwnerId == requestingAuthorId, ct);

        if (notebook is null)
            return false;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Delete entries (no CASCADE on this FK)
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM entries WHERE notebook_id = {0}", [notebookId], ct);

        // Delete jobs
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM jobs WHERE notebook_id = {0}", [notebookId], ct);

        // notebook_access has ON DELETE CASCADE, but explicit is fine too
        db.Notebooks.Remove(notebook);
        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
        return true;
    }

    public async Task<NotebookEntity?> RenameNotebookAsync(
        Guid notebookId, string newName, byte[] requestingAuthorId, CancellationToken ct)
    {
        var notebook = await db.Notebooks.FirstOrDefaultAsync(
            n => n.Id == notebookId && n.OwnerId == requestingAuthorId, ct);

        if (notebook is null)
            return null;

        notebook.Name = newName;
        await db.SaveChangesAsync(ct);
        return notebook;
    }

    public Task<int> CountEntriesAsync(Guid notebookId, CancellationToken ct)
        => db.Entries.CountAsync(e => e.NotebookId == notebookId, ct);

    public Task<int> CountParticipantsAsync(Guid notebookId, CancellationToken ct)
        => db.NotebookAccess.CountAsync(a => a.NotebookId == notebookId, ct);
}
