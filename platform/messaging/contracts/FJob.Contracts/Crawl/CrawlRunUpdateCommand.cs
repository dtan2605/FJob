namespace FJob.Contracts.Crawl;

public sealed class CrawlRunUpdateCommand
{
    public CrawlRunStatus Status { get; init; }
    public int AttemptCount { get; init; }
    public int ImportedJobs { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset? StartedAtUtc { get; init; }
    public DateTimeOffset? FinishedAtUtc { get; init; }
}
