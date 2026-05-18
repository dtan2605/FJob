namespace FJob.Contracts.Jobs;

public sealed class UpsertJobsResult
{
    public int ImportedCount { get; init; }
    public int InsertedCount { get; init; }
    public int UpdatedCount { get; init; }
}
