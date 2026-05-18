namespace FJob.Contracts.Search;

public sealed class SearchResultItem
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Salary { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal? SalaryMinMillions { get; init; }
    public decimal? SalaryMaxMillions { get; init; }
    public string[] Tags { get; init; } = [];
    public DateTimeOffset PostedAtUtc { get; init; }
    public double Score { get; init; }
}
