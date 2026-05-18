using FJob.Contracts.Crawl;

namespace FJob.JobCatalogService.Models;

public sealed class CrawlRunDocument
{
    public List<CrawlRunSummary> Items { get; init; } = [];
}
