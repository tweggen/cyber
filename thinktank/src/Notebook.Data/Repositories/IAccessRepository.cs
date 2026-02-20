using Notebook.Data.Entities;

namespace Notebook.Data.Repositories;

public interface IAccessRepository
{
    Task<NotebookAccessEntity?> GetAccessAsync(Guid notebookId, byte[] authorId, CancellationToken ct);
    Task GrantAccessAsync(Guid notebookId, byte[] authorId, bool read, bool write, CancellationToken ct);
    Task RevokeAccessAsync(Guid notebookId, byte[] authorId, CancellationToken ct);
    Task<List<NotebookAccessEntity>> ListAccessAsync(Guid notebookId, CancellationToken ct);
    Task<bool> IsOwnerAsync(Guid notebookId, byte[] authorId, CancellationToken ct);
}
