namespace Notebook.Server.Services;

public interface IEmbeddingService
{
    Task<double[]> EmbedQueryAsync(string text, CancellationToken ct);
}
