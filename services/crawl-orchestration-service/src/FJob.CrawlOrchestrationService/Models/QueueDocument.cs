namespace FJob.CrawlOrchestrationService.Models;

public sealed class QueueDocument
{
    public List<QueuedCrawlRequestState> Items { get; init; } = [];
}
