namespace Cyber.Client.Filters;

public sealed record FilterResult
{
    public required string Text { get; init; }
    public required string ContentType { get; init; }
}

public interface IContentFilter
{
    Task<FilterResult> FilterAsync(Stream input, string fileName, CancellationToken ct = default);
}
