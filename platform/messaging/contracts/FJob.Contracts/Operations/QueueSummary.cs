namespace FJob.Contracts.Operations;

public sealed class QueueSummary
{
    public int PendingCount { get; init; }
    public int ProcessingCount { get; init; }
    public int CompletedCount { get; init; }
    public int FailedCount { get; init; }
    public int TotalCount { get; init; }
    public DateTimeOffset? OldestPendingRequestedAtUtc { get; init; }
}
