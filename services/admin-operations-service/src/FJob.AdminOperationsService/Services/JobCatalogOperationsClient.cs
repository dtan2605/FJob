using System.Net.Http.Json;
using FJob.Contracts.Crawl;
using FJob.Observability;
using Microsoft.Extensions.Options;

namespace FJob.AdminOperationsService.Services;

public sealed class JobCatalogOperationsClient(
    HttpClient httpClient,
    IOptions<AdminOperationsOptions> options,
    ILogger<JobCatalogOperationsClient> logger)
{
    public async Task<IReadOnlyCollection<CrawlRunSummary>> GetCrawlRunsAsync(CancellationToken cancellationToken)
    {
        return await ResilienceExecutor.ExecuteAsync(
            "admin-get-crawl-runs",
            logger,
            async ct =>
            {
                var uri = new Uri(new Uri(options.Value.JobCatalogBaseUrl), "/api/crawl-runs");
                var payload = await httpClient.GetFromJsonAsync<IReadOnlyCollection<CrawlRunSummary>>(uri, ct);
                return payload ?? Array.Empty<CrawlRunSummary>();
            },
            cancellationToken);
    }
}
