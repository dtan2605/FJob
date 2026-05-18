using System.Net.Http.Json;
using FJob.Contracts.Jobs;
using FJob.Observability;
using Microsoft.Extensions.Options;

namespace FJob.JobSearchService.Services;

public sealed class JobCatalogExportClient(
    HttpClient httpClient,
    IOptions<SearchOptions> options,
    ILogger<JobCatalogExportClient> logger)
{
    public async Task<IReadOnlyCollection<JobRecord>> ExportJobsAsync(
        DateTimeOffset? updatedAfterUtc,
        CancellationToken cancellationToken)
    {
        return await ResilienceExecutor.ExecuteAsync(
            "job-catalog-export",
            logger,
            async ct =>
            {
                var uri = updatedAfterUtc.HasValue
                    ? new Uri(new Uri(options.Value.JobCatalogBaseUrl),
                        $"/api/jobs/export?updatedAfterUtc={Uri.EscapeDataString(updatedAfterUtc.Value.ToString("O"))}")
                    : new Uri(new Uri(options.Value.JobCatalogBaseUrl), "/api/jobs/export");

                var payload = await httpClient.GetFromJsonAsync<IReadOnlyCollection<JobRecord>>(uri, ct);
                return payload ?? Array.Empty<JobRecord>();
            },
            cancellationToken);
    }
}
