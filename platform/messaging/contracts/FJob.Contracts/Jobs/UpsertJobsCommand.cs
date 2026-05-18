using FJob.Contracts.Crawl;

namespace FJob.Contracts.Jobs;

public sealed class UpsertJobsCommand
{
    public Guid CrawlRunId { get; init; }
    public string TraceId { get; init; } = string.Empty;
    public IReadOnlyCollection<CrawledJobItem> Jobs { get; init; } = Array.Empty<CrawledJobItem>();
}
