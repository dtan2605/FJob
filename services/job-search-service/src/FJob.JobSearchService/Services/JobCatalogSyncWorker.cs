using FJob.Contracts.Search;
using Microsoft.Extensions.Options;

namespace FJob.JobSearchService.Services;

public sealed class JobCatalogSyncWorker(
    SearchSyncCoordinator coordinator,
    IOptions<SearchOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(3, options.Value.SyncIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            await coordinator.RunSyncAsync(fullRebuild: false, stoppingToken);
            await Task.Delay(interval, stoppingToken);
        }
    }
}
