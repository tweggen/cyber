namespace Notebook.Server.Services;

public record NormalizeResult(string Content, string ContentType, string? OriginalContentType);

public interface IContentNormalizer
{
    NormalizeResult Normalize(string content, string contentType);
}
