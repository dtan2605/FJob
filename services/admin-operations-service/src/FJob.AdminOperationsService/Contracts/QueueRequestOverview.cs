namespace FJob.AdminOperationsService.Contracts;

public sealed class QueueRequestOverview
{
    public Guid RequestId { get; init; }
    public Guid? CrawlRunId { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Keyword { get; init; } = string.Empty;
    public string TriggeredBy { get; init; } = string.Empty;
    public string TraceId { get; init; } = string.Empty;
    public DateTimeOffset RequestedAtUtc { get; init; }
    public int Status { get; init; }
    public int AttemptCount { get; init; }
    public DateTimeOffset NextAttemptAtUtc { get; init; }
    public string? ErrorMessage { get; init; }
}
