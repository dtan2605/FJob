namespace FJob.Contracts.Search;

public sealed class SearchDocument
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string NormalizedTitle { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string NormalizedCompany { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string NormalizedLocation { get; init; } = string.Empty;
    public string Salary { get; init; } = string.Empty;
    public decimal? SalaryMinMillions { get; init; }
    public decimal? SalaryMaxMillions { get; init; }
    public string Description { get; init; } = string.Empty;
    public string NormalizedDescription { get; init; } = string.Empty;
    public string[] Tags { get; init; } = [];
    public DateTimeOffset PostedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}
