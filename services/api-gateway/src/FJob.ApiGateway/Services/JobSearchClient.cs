using System.Net.Http.Json;
using FJob.Contracts.Search;
using Microsoft.Extensions.Options;
using FJob.Observability;

namespace FJob.ApiGateway.Services;

public sealed class JobSearchClient(
    HttpClient httpClient,
    IOptions<ApiGatewayOptions> options,
    ILogger<JobSearchClient> logger)
{
    public async Task<SearchResponse> SearchAsync(
        SearchQueryRequest request,
        CancellationToken cancellationToken)
    {
        return await ResilienceExecutor.ExecuteAsync(
            "job-search-request",
            logger,
            async ct =>
            {
                var uri = new Uri(new Uri(options.Value.JobSearchBaseUrl), "/api/search/jobs");
                using var response = await httpClient.PostAsJsonAsync(uri, request, ct);
                response.EnsureSuccessStatusCode();
                return (await response.Content.ReadFromJsonAsync<SearchResponse>(ct))!;
            },
            cancellationToken);
    }

    public async Task RebuildIndexAsync(CancellationToken cancellationToken)
    {
        await ResilienceExecutor.ExecuteAsync(
            "job-search-rebuild",
            logger,
            async ct =>
            {
                var uri = new Uri(new Uri(options.Value.JobSearchBaseUrl), "/api/search/admin/rebuild");
                using var response = await httpClient.PostAsync(uri, null, ct);
                response.EnsureSuccessStatusCode();
                return true;
            },
            cancellationToken);
    }
}
