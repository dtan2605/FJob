namespace FJob.Contracts.Crawl;

public sealed class CrawledJobItem
{
    public string SourceJobId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Salary { get; init; } = "Negotiable";
    public string Description { get; init; } = string.Empty;
    public string[] Tags { get; init; } = [];
    public DateTimeOffset PostedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
