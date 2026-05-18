namespace FJob.Contracts.Jobs;

public sealed class CreateCrawlRunCommand
{
    public string Source { get; init; } = string.Empty;
    public string Keyword { get; init; } = string.Empty;
    public string TriggeredBy { get; init; } = string.Empty;
    public string TraceId { get; init; } = string.Empty;
    public DateTimeOffset RequestedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
