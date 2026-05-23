namespace FJob.Contracts.Search;

public sealed class SearchQueryRequest
{
    public string Keyword { get; init; } = string.Empty;
    public string? Location { get; init; }
    public string[] Tags { get; init; } = [];
    public string[] Sources { get; init; } = [];
    public decimal? SalaryMinMillions { get; init; }
    public decimal? SalaryMaxMillions { get; init; }
    public int? PostedWithinDays { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int? MaxPages { get; init; }
    public string SortBy { get; init; } = "recent";
    // If true the search endpoint will trigger an immediate crawl (clear + enqueue).
    // Default false to avoid accidental re-crawls on page reload or route navigation.
    public bool TriggerCrawl { get; init; } = false;

    // When true, search will only filter against existing data in the catalog/index
    // and will not trigger any crawling or index rebuild.
    public bool FilterOnly { get; init; } = false;
}
