using FJob.Contracts.Search;

namespace FJob.JobSearchService.Models;

public sealed class SearchIndexDocument
{
    public List<SearchDocument> Items { get; init; } = [];
}
