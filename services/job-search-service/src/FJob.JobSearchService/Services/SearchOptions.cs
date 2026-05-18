namespace FJob.JobSearchService.Services;

public sealed class SearchOptions
{
    public const string SectionName = "Search";

    public string JobCatalogBaseUrl { get; init; } = "http://localhost:5101";
    public int SyncIntervalSeconds { get; init; } = 5;
    public int DefaultPageSize { get; init; } = 20;
    public int MaxPageSize { get; init; } = 100;
    public string? RedisConnectionString { get; init; }
}
