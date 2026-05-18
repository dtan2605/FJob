namespace FJob.Contracts.Search;

public sealed class SearchIndexSyncResult
{
    public int IndexedCount { get; init; }
    public int InsertedCount { get; init; }
    public int UpdatedCount { get; init; }
}
