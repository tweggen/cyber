namespace Cyber.Client.Filters;

public sealed class PlainTextFilter : IContentFilter
{
    public async Task<FilterResult> FilterAsync(Stream input, string fileName, CancellationToken ct = default)
    {
        using var reader = new StreamReader(input, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);
        return new FilterResult
        {
            Text = text,
            ContentType = "text/plain"
        };
    }
}
