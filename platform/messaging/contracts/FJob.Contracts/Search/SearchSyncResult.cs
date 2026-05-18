namespace FJob.Contracts.Search;

public sealed class SearchSyncResult
{
    public bool Success { get; init; }
    public bool FullRebuild { get; init; }
    public int IndexedCount { get; init; }
    public int InsertedCount { get; init; }
    public int UpdatedCount { get; init; }
    public DateTimeOffset AttemptedAtUtc { get; init; }
    public DateTimeOffset? WatermarkUpdatedAtUtc { get; init; }
    public string? ErrorMessage { get; init; }
}
