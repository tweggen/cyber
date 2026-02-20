using Notebook.Data.Entities;

namespace Notebook.Data.Repositories;

public interface INotebookRepository
{
    Task<List<NotebookEntity>> ListNotebooksAsync(byte[] authorId, CancellationToken ct);
    Task<NotebookEntity> CreateNotebookAsync(string name, byte[] ownerId, CancellationToken ct,
        string classification = "INTERNAL", List<string>? compartments = null);
    Task<bool> DeleteNotebookAsync(Guid notebookId, byte[] requestingAuthorId, CancellationToken ct);
    Task<NotebookEntity?> RenameNotebookAsync(Guid notebookId, string newName, byte[] requestingAuthorId, CancellationToken ct);
    Task<int> CountEntriesAsync(Guid notebookId, CancellationToken ct);
    Task<int> CountParticipantsAsync(Guid notebookId, CancellationToken ct);
}
