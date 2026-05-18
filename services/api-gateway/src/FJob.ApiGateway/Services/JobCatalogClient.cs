using FJob.Observability;
using Microsoft.Extensions.Options;

namespace FJob.ApiGateway.Services;

public sealed class JobCatalogClient(
    HttpClient httpClient,
    IOptions<ApiGatewayOptions> options,
    ILogger<JobCatalogClient> logger)
{
    public async Task ClearJobsAsync(CancellationToken cancellationToken)
    {
        await ResilienceExecutor.ExecuteAsync(
            "job-catalog-clear",
            logger,
            async ct =>
            {
                var uri = new Uri(new Uri(options.Value.JobCatalogBaseUrl), "/api/admin/jobs/clear");
                using var response = await httpClient.PostAsync(uri, null, ct);
                response.EnsureSuccessStatusCode();
                return true;
            },
            cancellationToken);
    }
}
