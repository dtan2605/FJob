using System.Net.Http.Json;
using FJob.Contracts.Crawl;
using FJob.Contracts.Jobs;
using FJob.Observability;
using Microsoft.Extensions.Options;

namespace FJob.CrawlOrchestrationService.Services;

public sealed class JobCatalogClient(
    HttpClient httpClient,
    IOptions<OrchestrationOptions> options,
    ILogger<JobCatalogClient> logger)
{
    public async Task<Guid> CreateCrawlRunAsync(CreateCrawlRunCommand command, CancellationToken cancellationToken)
    {
        return await ResilienceExecutor.ExecuteAsync(
            "catalog-create-crawl-run",
            logger,
            async ct =>
            {
                using var response = await httpClient.PostAsJsonAsync(
                    BuildUri("/api/crawl-runs"),
                    command,
                    ct);
                response.EnsureSuccessStatusCode();

                var payload = await response.Content.ReadFromJsonAsync<CrawlRunSummary>(ct);
                return payload!.Id;
            },
            cancellationToken);
    }

    public async Task UpdateCrawlRunAsync(Guid id, CrawlRunUpdateCommand command, CancellationToken cancellationToken)
    {
        await ResilienceExecutor.ExecuteAsync(
            "catalog-update-crawl-run",
            logger,
            async ct =>
            {
                using var response = await httpClient.PutAsJsonAsync(
                    BuildUri($"/api/crawl-runs/{id}/status"),
                    command,
                    ct);
                response.EnsureSuccessStatusCode();
                return true;
            },
            cancellationToken);
    }

    public async Task<UpsertJobsResult> UpsertJobsAsync(UpsertJobsCommand command, CancellationToken cancellationToken)
    {
        return await ResilienceExecutor.ExecuteAsync(
            "catalog-upsert-jobs",
            logger,
            async ct =>
            {
                using var response = await httpClient.PostAsJsonAsync(
                    BuildUri("/api/jobs/upsert-batch"),
                    command,
                    ct);
                response.EnsureSuccessStatusCode();
                return (await response.Content.ReadFromJsonAsync<UpsertJobsResult>(ct))!;
            },
            cancellationToken);
    }

    private Uri BuildUri(string relativePath) =>
        new(new Uri(options.Value.JobCatalogBaseUrl), relativePath);
}
