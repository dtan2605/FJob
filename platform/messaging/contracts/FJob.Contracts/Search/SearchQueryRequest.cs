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
}
