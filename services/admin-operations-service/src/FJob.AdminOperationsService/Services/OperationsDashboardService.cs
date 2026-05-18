using FJob.AdminOperationsService.Contracts;
using FJob.Contracts.Crawl;

namespace FJob.AdminOperationsService.Services;

public sealed class OperationsDashboardService(
    OrchestrationOperationsClient orchestrationClient,
    JobCatalogOperationsClient jobCatalogClient,
    AdminAuditLogService auditLogService)
{
    public async Task<AdminDashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var queueSummaryTask = orchestrationClient.GetQueueSummaryAsync(cancellationToken);
        var sourcesTask = orchestrationClient.GetSourcesAsync(cancellationToken);
        var queueItemsTask = orchestrationClient.GetQueueItemsAsync(cancellationToken);
        var crawlRunsTask = jobCatalogClient.GetCrawlRunsAsync(cancellationToken);
        var auditLogsTask = auditLogService.GetRecentAsync(15, cancellationToken);

        await Task.WhenAll(queueSummaryTask, sourcesTask, queueItemsTask, crawlRunsTask, auditLogsTask);

        var crawlRuns = crawlRunsTask.Result
            .OrderByDescending(x => x.RequestedAtUtc)
            .Take(20)
            .ToArray();

        var failedSources = crawlRuns
            .Where(x => x.Status == CrawlRunStatus.Failed)
            .GroupBy(x => x.Source, StringComparer.OrdinalIgnoreCase)
            .Select(group => new FailedSourceSummary
            {
                Source = group.Key,
                FailureCount = group.Count()
            })
            .OrderByDescending(x => x.FailureCount)
            .ToArray();

        return new AdminDashboardSnapshot
        {
            QueueSummary = queueSummaryTask.Result,
            Sources = sourcesTask.Result,
            QueueItems = queueItemsTask.Result.OrderByDescending(x => x.RequestedAtUtc).Take(20).ToArray(),
            RecentCrawlRuns = crawlRuns,
            FailedSources = failedSources,
            RecentAuditLogs = auditLogsTask.Result
        };
    }
}
