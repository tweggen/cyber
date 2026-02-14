using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage;
using Notebook.Core.Types;

namespace Notebook.Data.Repositories;

public interface IEntryRepository
{
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct);
    Task<bool> NotebookExistsAsync(Guid notebookId, CancellationToken ct);
    Task<Entry> InsertEntryAsync(Guid notebookId, byte[] authorId, NewEntry entry, CancellationToken ct);
    Task<bool> UpdateEntryClaimsAsync(Guid entryId, Guid notebookId, List<Claim> claims, CancellationToken ct);
    Task<List<(Guid Id, List<Claim> Claims)>> FindTopicIndicesAsync(Guid notebookId, CancellationToken ct);
    Task AppendComparisonAsync(Guid entryId, JsonElement comparison, CancellationToken ct);
    Task UpdateEntryTopicAsync(Guid entryId, string topic, CancellationToken ct);
}
