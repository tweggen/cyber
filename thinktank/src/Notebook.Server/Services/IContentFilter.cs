namespace Notebook.Server.Services;

public record FilterResult(string Content, string? DetectedSource);

public interface IContentFilter
{
    string SourceName { get; }
    bool CanHandle(string content, string? sourceHint);
    FilterResult Filter(string content);
}

public interface IContentFilterPipeline
{
    FilterResult Apply(string content, string? sourceHint);
}
