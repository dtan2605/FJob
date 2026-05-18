namespace FJob.Contracts.Crawl;

public sealed class CrawlRequestMessage
{
    public Guid RequestId { get; init; }
    public Guid? CrawlRunId { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Keyword { get; init; } = string.Empty;
    public string TriggeredBy { get; init; } = "manual";
    public DateTimeOffset RequestedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string TraceId { get; init; } = string.Empty;

    // Advanced filtering options
    public string? Location { get; init; }
    public string? SalaryRange { get; init; }
    public string[]? Tags { get; init; }
    public string? ExperienceLevel { get; init; }
    public string? JobType { get; init; }

    // Proxy configuration for resilient crawling
    public string[]? ProxyUrls { get; init; }

    // Runtime crawl limits
    public int? MaxPages { get; init; }
}
