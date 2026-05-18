using FJob.Contracts.Crawl;
using FJob.Contracts.Operations;

namespace FJob.AdminOperationsService.Contracts;

public sealed class AdminDashboardSnapshot
{
    public QueueSummary QueueSummary { get; init; } = new();
    public IReadOnlyCollection<SourceControlState> Sources { get; init; } = Array.Empty<SourceControlState>();
    public IReadOnlyCollection<CrawlRunSummary> RecentCrawlRuns { get; init; } = Array.Empty<CrawlRunSummary>();
    public IReadOnlyCollection<QueueRequestOverview> QueueItems { get; init; } = Array.Empty<QueueRequestOverview>();
    public IReadOnlyCollection<FailedSourceSummary> FailedSources { get; init; } = Array.Empty<FailedSourceSummary>();
    public IReadOnlyCollection<AdminAuditLogEntry> RecentAuditLogs { get; init; } = Array.Empty<AdminAuditLogEntry>();
}
