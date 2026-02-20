using Microsoft.EntityFrameworkCore;
using Notebook.Data.Entities;

namespace Notebook.Data.Repositories;

public class AccessRepository(NotebookDbContext db) : IAccessRepository
{
    public Task<NotebookAccessEntity?> GetAccessAsync(Guid notebookId, byte[] authorId, CancellationToken ct)
        => db.NotebookAccess.FirstOrDefaultAsync(
            a => a.NotebookId == notebookId && a.AuthorId == authorId, ct);

    public async Task GrantAccessAsync(Guid notebookId, byte[] authorId, bool read, bool write, CancellationToken ct)
    {
        // Ensure the author exists in the authors table
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO authors (id, public_key) VALUES ({0}, {1}) ON CONFLICT DO NOTHING",
            [authorId, authorId], ct);

        var existing = await db.NotebookAccess.FirstOrDefaultAsync(
            a => a.NotebookId == notebookId && a.AuthorId == authorId, ct);

        if (existing is not null)
        {
            existing.Read = read;
            existing.Write = write;
            existing.Granted = DateTimeOffset.UtcNow;
        }
        else
        {
            db.NotebookAccess.Add(new NotebookAccessEntity
            {
                NotebookId = notebookId,
                AuthorId = authorId,
                Read = read,
                Write = write,
                Granted = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeAccessAsync(Guid notebookId, byte[] authorId, CancellationToken ct)
    {
        var access = await db.NotebookAccess.FirstOrDefaultAsync(
            a => a.NotebookId == notebookId && a.AuthorId == authorId, ct);

        if (access is not null)
        {
            db.NotebookAccess.Remove(access);
            await db.SaveChangesAsync(ct);
        }
    }

    public Task<List<NotebookAccessEntity>> ListAccessAsync(Guid notebookId, CancellationToken ct)
        => db.NotebookAccess
            .Where(a => a.NotebookId == notebookId)
            .OrderBy(a => a.Granted)
            .ToListAsync(ct);

    public Task<bool> IsOwnerAsync(Guid notebookId, byte[] authorId, CancellationToken ct)
        => db.Notebooks.AnyAsync(
            n => n.Id == notebookId && n.OwnerId == authorId, ct);
}
