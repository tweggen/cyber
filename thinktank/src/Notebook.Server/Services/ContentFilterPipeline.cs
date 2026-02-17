namespace Notebook.Server.Services;

public sealed class ContentFilterPipeline : IContentFilterPipeline
{
    private readonly IReadOnlyList<IContentFilter> _filters;

    public ContentFilterPipeline(IEnumerable<IContentFilter> filters)
    {
        _filters = filters.ToList();
    }

    public FilterResult Apply(string content, string? sourceHint)
    {
        // Explicit hint gets priority
        if (sourceHint is not null)
        {
            var exact = _filters.FirstOrDefault(f =>
                f.SourceName.Equals(sourceHint, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
                return exact.Filter(content);
        }

        // Auto-detect fallback
        foreach (var filter in _filters)
        {
            if (filter.CanHandle(content, sourceHint))
                return filter.Filter(content);
        }

        return new FilterResult(content, null);
    }
}
