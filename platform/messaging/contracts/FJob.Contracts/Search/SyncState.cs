namespace FJob.Contracts.Search;

public sealed class SyncState
{
    public DateTimeOffset? LastSuccessfulSyncUtc { get; init; }
    public DateTimeOffset? LastAttemptedSyncUtc { get; init; }
    public DateTimeOffset? LastIndexedJobUpdatedAtUtc { get; init; }
    public int LastIndexedCount { get; init; }
    public string? LastError { get; init; }
}
