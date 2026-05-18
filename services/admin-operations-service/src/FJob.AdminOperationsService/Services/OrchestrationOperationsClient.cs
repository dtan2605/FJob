using System.Net;
using System.Net.Http.Json;
using FJob.AdminOperationsService.Contracts;
using FJob.Contracts.Operations;
using FJob.Observability;
using Microsoft.Extensions.Options;

namespace FJob.AdminOperationsService.Services;

public sealed class OrchestrationOperationsClient(
    HttpClient httpClient,
    IOptions<AdminOperationsOptions> options,
    ILogger<OrchestrationOperationsClient> logger)
{
    public async Task<IReadOnlyCollection<QueueRequestOverview>> GetQueueItemsAsync(CancellationToken cancellationToken)
    {
        return await ResilienceExecutor.ExecuteAsync(
            "admin-get-queue-items",
            logger,
            async ct =>
            {
                var uri = BuildUri("/api/crawl-requests");
                var payload = await httpClient.GetFromJsonAsync<IReadOnlyCollection<QueueRequestOverview>>(uri, ct);
                return payload ?? Array.Empty<QueueRequestOverview>();
            },
            cancellationToken);
    }

    public async Task<QueueSummary> GetQueueSummaryAsync(CancellationToken cancellationToken)
    {
        return await ResilienceExecutor.ExecuteAsync(
            "admin-get-queue-summary",
            logger,
            async ct =>
            {
                var uri = BuildUri("/api/operations/queue-summary");
                return (await httpClient.GetFromJsonAsync<QueueSummary>(uri, ct)) ?? new QueueSummary();
            },
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<SourceControlState>> GetSourcesAsync(CancellationToken cancellationToken)
    {
        return await ResilienceExecutor.ExecuteAsync(
            "admin-get-sources",
            logger,
            async ct =>
            {
                var uri = BuildUri("/api/operations/sources");
                var payload = await httpClient.GetFromJsonAsync<IReadOnlyCollection<SourceControlState>>(uri, ct);
                return payload ?? Array.Empty<SourceControlState>();
            },
            cancellationToken);
    }

    public async Task<AdminActionResult<QueueRequestOverview>> TriggerManualCrawlAsync(
        AdminManualCrawlRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await ResilienceExecutor.ExecuteAsync(
            "admin-trigger-manual-crawl",
            logger,
            async ct =>
            {
                var uri = BuildUri("/api/crawl-requests");
                return await httpClient.PostAsJsonAsync(uri, new
                {
                    request.Source,
                    request.Keyword,
                    request.TriggeredBy,
                    request.Location,
                    request.SalaryRange,
                    request.Tags,
                    request.ExperienceLevel,
                    request.JobType,
                    request.ProxyUrls
                }, ct);
            },
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new AdminActionResult<QueueRequestOverview>
            {
                Success = true,
                StatusCode = (int)response.StatusCode,
                Data = await response.Content.ReadFromJsonAsync<QueueRequestOverview>(cancellationToken)
            };
        }

        return new AdminActionResult<QueueRequestOverview>
        {
            Success = false,
            StatusCode = (int)response.StatusCode,
            Message = await response.Content.ReadAsStringAsync(cancellationToken)
        };
    }

    public async Task<bool> RetryFailedRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        using var response = await ResilienceExecutor.ExecuteAsync(
            "admin-retry-failed-request",
            logger,
            async ct =>
            {
                var uri = BuildUri($"/api/operations/crawl-requests/{requestId}/retry");
                return await httpClient.PostAsync(uri, content: null, ct);
            },
            cancellationToken);
        return response.StatusCode != HttpStatusCode.NotFound && response.IsSuccessStatusCode;
    }

    public async Task<SourceControlState?> PauseSourceAsync(
        string source,
        string? reason,
        CancellationToken cancellationToken)
    {
        using var response = await ResilienceExecutor.ExecuteAsync(
            "admin-pause-source",
            logger,
            async ct =>
            {
                var uri = BuildUri($"/api/operations/sources/{Uri.EscapeDataString(source)}/pause");
                return await httpClient.PostAsJsonAsync(uri, new UpdateSourceStateRequest
                {
                    Reason = reason
                }, ct);
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SourceControlState>(cancellationToken);
    }

    public async Task<SourceControlState?> ResumeSourceAsync(string source, CancellationToken cancellationToken)
    {
        using var response = await ResilienceExecutor.ExecuteAsync(
            "admin-resume-source",
            logger,
            async ct =>
            {
                var uri = BuildUri($"/api/operations/sources/{Uri.EscapeDataString(source)}/resume");
                return await httpClient.PostAsync(uri, content: null, ct);
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SourceControlState>(cancellationToken);
    }

    private Uri BuildUri(string relativePath) => new(new Uri(options.Value.OrchestrationBaseUrl), relativePath);
}
