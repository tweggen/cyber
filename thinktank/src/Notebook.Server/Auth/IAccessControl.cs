namespace Notebook.Server.Auth;

public interface IAccessControl
{
    Task<IResult?> RequireReadAsync(Guid notebookId, byte[] authorId, CancellationToken ct);
    Task<IResult?> RequireWriteAsync(Guid notebookId, byte[] authorId, CancellationToken ct);
    Task<IResult?> RequireOwnerAsync(Guid notebookId, byte[] authorId, CancellationToken ct);
}
