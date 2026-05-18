namespace FJob.Contracts.Crawl;

public sealed class CrawlRunSummary
{
    public Guid Id { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Keyword { get; init; } = string.Empty;
    public string TriggeredBy { get; init; } = string.Empty;
    public CrawlRunStatus Status { get; init; }
    public int AttemptCount { get; init; }
    public int ImportedJobs { get; init; }
    public string? ErrorMessage { get; init; }
    public string TraceId { get; init; } = string.Empty;
    public DateTimeOffset RequestedAtUtc { get; init; }
    public DateTimeOffset? StartedAtUtc { get; init; }
    public DateTimeOffset? FinishedAtUtc { get; init; }
}
