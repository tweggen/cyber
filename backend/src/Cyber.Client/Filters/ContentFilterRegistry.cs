namespace Cyber.Client.Filters;

public sealed class ContentFilterRegistry
{
    private readonly Dictionary<string, IContentFilter> _filters = new(StringComparer.OrdinalIgnoreCase);

    public ContentFilterRegistry()
    {
        var htmlFilter = new HtmlContentFilter();
        var plainTextFilter = new PlainTextFilter();

        Register(".html", htmlFilter);
        Register(".htm", htmlFilter);
        Register(".txt", plainTextFilter);
        Register(".md", plainTextFilter);
        Register(".markdown", plainTextFilter);
    }

    public void Register(string extension, IContentFilter filter)
    {
        _filters[extension] = filter;
    }

    public IContentFilter? GetFilter(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext))
            return null;

        return _filters.GetValueOrDefault(ext);
    }

    public bool IsSupported(string fileName)
    {
        return GetFilter(fileName) != null;
    }

    public IReadOnlyCollection<string> SupportedExtensions => _filters.Keys;
}
