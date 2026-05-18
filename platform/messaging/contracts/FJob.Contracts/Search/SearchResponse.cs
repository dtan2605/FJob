namespace FJob.Contracts.Search;

public sealed class SearchResponse
{
    public IReadOnlyCollection<SearchResultItem> Items { get; init; } = Array.Empty<SearchResultItem>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}
