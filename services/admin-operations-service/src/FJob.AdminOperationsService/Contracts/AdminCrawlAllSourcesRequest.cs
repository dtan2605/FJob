namespace FJob.AdminOperationsService.Contracts;

public sealed class AdminCrawlAllSourcesRequest
{
    public string Keyword { get; init; } = string.Empty;
    public string TriggeredBy { get; init; } = "admin-dashboard";

    // Advanced filtering options
    public string? Location { get; init; }
    public string? SalaryRange { get; init; }
    public string[]? Tags { get; init; }
    public string? ExperienceLevel { get; init; }
    public string? JobType { get; init; }

    // Proxy configuration for resilient crawling
    public string[]? ProxyUrls { get; init; }
}