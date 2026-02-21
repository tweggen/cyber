namespace Cyber.Client.Crawlers;

public interface ICrawler
{
    string Type { get; }
    Task CrawlAsync(CrawlerConfig config, IProgress<string>? progress = null, CancellationToken ct = default);
}
