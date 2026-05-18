using FJob.Contracts.Jobs;
using FJob.Contracts.Search;

namespace FJob.JobSearchService.Services;

public interface ISearchIndexStore
{
    Task<IReadOnlyCollection<SearchDocument>> GetAllAsync(CancellationToken cancellationToken);
    Task<SearchIndexSyncResult> UpsertBatchAsync(IReadOnlyCollection<JobRecord> jobs, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
    Task<SyncState> GetSyncStateAsync(CancellationToken cancellationToken);
    Task SaveSyncStateAsync(SyncState state, CancellationToken cancellationToken);
}
