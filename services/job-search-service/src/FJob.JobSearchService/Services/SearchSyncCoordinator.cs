using FJob.Contracts.Search;

namespace FJob.JobSearchService.Services;

public sealed class SearchSyncCoordinator(
    ILogger<SearchSyncCoordinator> logger,
    ISearchIndexStore store,
    JobCatalogExportClient exportClient)
{
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public async Task<SearchSyncResult> RunSyncAsync(bool fullRebuild, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var attemptedAtUtc = DateTimeOffset.UtcNow;
            var syncState = await store.GetSyncStateAsync(cancellationToken);

            if (fullRebuild)
            {
                await store.ClearAsync(cancellationToken);
                syncState = new SyncState();
            }

            var jobs = await exportClient.ExportJobsAsync(
                fullRebuild ? null : syncState.LastIndexedJobUpdatedAtUtc,
                cancellationToken);

            var indexResult = jobs.Count > 0
                ? await store.UpsertBatchAsync(jobs, cancellationToken)
                : new SearchIndexSyncResult();

            var watermark = jobs.Count > 0
                ? jobs.Max(job => job.UpdatedAtUtc)
                : syncState.LastIndexedJobUpdatedAtUtc;

            var updatedState = new SyncState
            {
                LastAttemptedSyncUtc = attemptedAtUtc,
                LastSuccessfulSyncUtc = attemptedAtUtc,
                LastIndexedJobUpdatedAtUtc = watermark,
                LastIndexedCount = indexResult.IndexedCount,
                LastError = null
            };

            await store.SaveSyncStateAsync(updatedState, cancellationToken);

            logger.LogInformation(
                "Search sync completed. Full rebuild: {FullRebuild}. Indexed: {IndexedCount}",
                fullRebuild,
                indexResult.IndexedCount);

            return new SearchSyncResult
            {
                Success = true,
                FullRebuild = fullRebuild,
                IndexedCount = indexResult.IndexedCount,
                InsertedCount = indexResult.InsertedCount,
                UpdatedCount = indexResult.UpdatedCount,
                AttemptedAtUtc = attemptedAtUtc,
                WatermarkUpdatedAtUtc = watermark
            };
        }
        catch (Exception ex)
        {
            var attemptedAtUtc = DateTimeOffset.UtcNow;
            var currentState = await store.GetSyncStateAsync(cancellationToken);
            await store.SaveSyncStateAsync(new SyncState
            {
                LastAttemptedSyncUtc = attemptedAtUtc,
                LastSuccessfulSyncUtc = currentState.LastSuccessfulSyncUtc,
                LastIndexedJobUpdatedAtUtc = currentState.LastIndexedJobUpdatedAtUtc,
                LastIndexedCount = currentState.LastIndexedCount,
                LastError = ex.Message
            }, cancellationToken);

            logger.LogError(ex, "Search sync failed. Full rebuild: {FullRebuild}", fullRebuild);

            return new SearchSyncResult
            {
                Success = false,
                FullRebuild = fullRebuild,
                AttemptedAtUtc = attemptedAtUtc,
                WatermarkUpdatedAtUtc = currentState.LastIndexedJobUpdatedAtUtc,
                ErrorMessage = ex.Message
            };
        }
        finally
        {
            _mutex.Release();
        }
    }
}
