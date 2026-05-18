using System.Text.Json;

namespace FJob.CrawlOrchestrationService.Models;

public sealed record QueuedCrawlRequestState
{
    public Guid RequestId { get; init; }
    public Guid? CrawlRunId { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Keyword { get; init; } = string.Empty;
    public string TriggeredBy { get; init; } = string.Empty;
    public string TraceId { get; init; } = string.Empty;
    public DateTimeOffset RequestedAtUtc { get; init; }
    public string? Location { get; init; }
    public string? SalaryRange { get; init; }
    public string? TagsJson { get; init; }
    public string[]? Tags => string.IsNullOrWhiteSpace(TagsJson)
        ? null
        : JsonSerializer.Deserialize<string[]>(TagsJson);
    public string? ExperienceLevel { get; init; }
    public string? JobType { get; init; }
    public string? ProxyUrlsJson { get; init; }
    public string[]? ProxyUrls => string.IsNullOrWhiteSpace(ProxyUrlsJson)
        ? null
        : JsonSerializer.Deserialize<string[]>(ProxyUrlsJson);
    public int? MaxPages { get; init; }
    public QueueRequestStatus Status { get; init; }
    public int AttemptCount { get; init; }
    public DateTimeOffset NextAttemptAtUtc { get; init; }
    public string? ErrorMessage { get; init; }
}
