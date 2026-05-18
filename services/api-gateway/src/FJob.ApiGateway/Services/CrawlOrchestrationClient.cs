using System.Net.Http.Json;
using FJob.Contracts.Crawl;
using Microsoft.Extensions.Options;
using FJob.Observability;

namespace FJob.ApiGateway.Services;

public sealed class CrawlOrchestrationClient(
    HttpClient httpClient,
    IOptions<ApiGatewayOptions> options,
    RabbitMqPublisher rabbitMqPublisher,
    ILogger<CrawlOrchestrationClient> logger)
{
    public async Task<QueuedCrawlRequestState> EnqueueAsync(
        CrawlRequestMessage request,
        bool preferDirectHttp,
        CancellationToken cancellationToken)
    {
        return await ResilienceExecutor.ExecuteAsync(
            "crawl-orchestration-enqueue",
            logger,
            async ct =>
            {
                if (options.Value.UseRabbitMq && !preferDirectHttp)
                {
                    return await rabbitMqPublisher.PublishAsync(request, ct);
                }

                var uri = new Uri(new Uri(options.Value.CrawlOrchestrationBaseUrl), "/api/crawl-requests");
                using var response = await httpClient.PostAsJsonAsync(uri, request, ct);
                response.EnsureSuccessStatusCode();
                return (await response.Content.ReadFromJsonAsync<QueuedCrawlRequestState>(ct))!;
            },
            cancellationToken);
    }

    public async Task<QueuedCrawlRequestState?> GetRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        return await ResilienceExecutor.ExecuteAsync(
            "crawl-orchestration-get-request",
            logger,
            async ct =>
            {
                var uri = new Uri(new Uri(options.Value.CrawlOrchestrationBaseUrl), $"/api/crawl-requests/{requestId}");
                using var response = await httpClient.GetAsync(uri, ct);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<QueuedCrawlRequestState>(ct);
            },
            cancellationToken);
    }
}

public sealed record QueuedCrawlRequestState
{
    public Guid RequestId { get; init; }
    public string Source { get; init; } = string.Empty;
    public string TriggeredBy { get; init; } = string.Empty;
    public string TraceId { get; init; } = string.Empty;
    public DateTimeOffset RequestedAtUtc { get; init; }
    public string? Location { get; init; }
    public string? SalaryRange { get; init; }
    public string[]? Tags { get; init; }
    public string? ExperienceLevel { get; init; }
    public string? JobType { get; init; }
    public int? MaxPages { get; init; }
    public QueueRequestStatus Status { get; init; }
    public int AttemptCount { get; init; }
    public DateTimeOffset NextAttemptAtUtc { get; init; }
    public string? ErrorMessage { get; init; }
}

public enum QueueRequestStatus
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4
}
