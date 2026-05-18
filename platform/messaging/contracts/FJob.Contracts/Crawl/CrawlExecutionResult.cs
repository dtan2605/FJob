namespace FJob.Contracts.Crawl;

public sealed class CrawlExecutionResult
{
    public string Source { get; init; } = string.Empty;
    public string Keyword { get; init; } = string.Empty;
    public string Strategy { get; init; } = string.Empty;
    public string TraceId { get; init; } = string.Empty;
    public IReadOnlyCollection<CrawledJobItem> Jobs { get; init; } = Array.Empty<CrawledJobItem>();
}
