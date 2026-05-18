using FJob.Contracts.Crawl;
using FJob.Contracts.Jobs;
using FJob.CrawlOrchestrationService.Models;
using Microsoft.Extensions.Options;

namespace FJob.CrawlOrchestrationService.Services;

public sealed class CrawlDispatcherWorker(
    ILogger<CrawlDispatcherWorker> logger,
    CrawlRequestQueue queue,
    SourceControlRepository sourceControlRepository,
    JobCatalogClient jobCatalogClient,
    PythonCrawlerExecutor pythonCrawlerExecutor,
    IOptions<OrchestrationOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(2, options.Value.PollingIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            var next = await queue.TryAcquireNextAsync(stoppingToken);
            if (next is null)
            {
                await Task.Delay(interval, stoppingToken);
                continue;
            }

            if (await sourceControlRepository.IsPausedAsync(next.Source, stoppingToken))
            {
                await queue.ReturnToPendingAsync(
                    next.RequestId,
                    $"Source {next.Source} is paused.",
                    interval,
                    stoppingToken);
                await Task.Delay(interval, stoppingToken);
                continue;
            }

            try
            {
                var startedAtUtc = DateTimeOffset.UtcNow;
                var runId = next.CrawlRunId ?? await jobCatalogClient.CreateCrawlRunAsync(new CreateCrawlRunCommand
                {
                    Source = next.Source,
                    Keyword = next.Keyword,
                    TriggeredBy = next.TriggeredBy,
                    TraceId = next.TraceId,
                    RequestedAtUtc = next.RequestedAtUtc
                }, stoppingToken);

                await queue.AttachRunAsync(next.RequestId, runId, stoppingToken);

                await jobCatalogClient.UpdateCrawlRunAsync(runId, new CrawlRunUpdateCommand
                {
                    Status = CrawlRunStatus.Running,
                    AttemptCount = next.AttemptCount + 1,
                    ImportedJobs = 0,
                    StartedAtUtc = startedAtUtc
                }, stoppingToken);

                var executionResult = await pythonCrawlerExecutor.ExecuteAsync(new CrawlRequestMessage
                {
                    RequestId = next.RequestId,
                    CrawlRunId = runId,
                    Source = next.Source,
                    Keyword = next.Keyword,
                    TriggeredBy = next.TriggeredBy,
                    RequestedAtUtc = next.RequestedAtUtc,
                    TraceId = next.TraceId,
                    Location = next.Location,
                    SalaryRange = next.SalaryRange,
                    Tags = next.Tags,
                    ExperienceLevel = next.ExperienceLevel,
                    JobType = next.JobType,
                    ProxyUrls = next.ProxyUrls,
                    MaxPages = next.MaxPages
                }, stoppingToken);

                var upsertResult = await jobCatalogClient.UpsertJobsAsync(new UpsertJobsCommand
                {
                    CrawlRunId = runId,
                    TraceId = next.TraceId,
                    Jobs = executionResult.Jobs
                }, stoppingToken);

                await jobCatalogClient.UpdateCrawlRunAsync(runId, new CrawlRunUpdateCommand
                {
                    Status = CrawlRunStatus.Completed,
                    AttemptCount = next.AttemptCount + 1,
                    ImportedJobs = upsertResult.ImportedCount,
                    StartedAtUtc = startedAtUtc,
                    FinishedAtUtc = DateTimeOffset.UtcNow
                }, stoppingToken);

                await queue.MarkCompletedAsync(next.RequestId, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed processing crawl request {RequestId}", next.RequestId);
                await queue.MarkFailedOrRetryAsync(next.RequestId, ex.Message, options.Value.MaxAttempts, stoppingToken);

                if (next.CrawlRunId.HasValue)
                {
                    var latest = await queue.GetByIdAsync(next.RequestId, stoppingToken);
                    if (latest is not null && latest.Status == QueueRequestStatus.Failed)
                    {
                        await jobCatalogClient.UpdateCrawlRunAsync(next.CrawlRunId.Value, new CrawlRunUpdateCommand
                        {
                            Status = CrawlRunStatus.Failed,
                            AttemptCount = latest.AttemptCount,
                            ImportedJobs = 0,
                            ErrorMessage = ex.Message,
                            FinishedAtUtc = DateTimeOffset.UtcNow
                        }, stoppingToken);
                    }
                }
            }
        }
    }
}
