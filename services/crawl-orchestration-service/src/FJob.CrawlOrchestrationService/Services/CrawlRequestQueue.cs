using System.Text.Json;
using Dapper;
using FJob.Contracts.Crawl;
using FJob.Contracts.Operations;
using FJob.CrawlOrchestrationService.Models;

namespace FJob.CrawlOrchestrationService.Services;

public sealed class CrawlRequestQueue(MySqlConnectionFactory connectionFactory)
{
    public async Task<QueuedCrawlRequestState> EnqueueAsync(
        CrawlRequestMessage message,
        CancellationToken cancellationToken)
    {
        var state = new QueuedCrawlRequestState
        {
            RequestId = message.RequestId,
            Source = message.Source,
            Keyword = message.Keyword,
            TriggeredBy = message.TriggeredBy,
            TraceId = message.TraceId,
            RequestedAtUtc = message.RequestedAtUtc,
            Location = message.Location,
            SalaryRange = message.SalaryRange,
            TagsJson = message.Tags is null ? null : JsonSerializer.Serialize(message.Tags),
            ExperienceLevel = message.ExperienceLevel,
            JobType = message.JobType,
            ProxyUrlsJson = message.ProxyUrls is null ? null : JsonSerializer.Serialize(message.ProxyUrls),
            MaxPages = message.MaxPages,
            Status = QueueRequestStatus.Pending,
            NextAttemptAtUtc = DateTimeOffset.UtcNow
        };

        const string sql = """
            INSERT INTO crawl_orchestration_queue_requests (
                request_id,
                crawl_run_id,
                source,
                keyword,
                triggered_by,
                trace_id,
                requested_at_utc,
                location,
                salary_range,
                tags_json,
                experience_level,
                job_type,
                proxy_urls_json,
                max_pages,
                status,
                attempt_count,
                next_attempt_at_utc,
                error_message
            )
            VALUES (
                @RequestId,
                @CrawlRunId,
                @Source,
                @Keyword,
                @TriggeredBy,
                @TraceId,
                @RequestedAtUtc,
                @Location,
                @SalaryRange,
                @TagsJson,
                @ExperienceLevel,
                @JobType,
                @ProxyUrlsJson,
                @MaxPages,
                @Status,
                @AttemptCount,
                @NextAttemptAtUtc,
                @ErrorMessage
            )
            ON DUPLICATE KEY UPDATE
                request_id = request_id;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, state, cancellationToken: cancellationToken));
        return state;
    }

    public async Task<IReadOnlyCollection<QueuedCrawlRequestState>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                request_id AS RequestId,
                crawl_run_id AS CrawlRunId,
                source AS Source,
                keyword AS Keyword,
                triggered_by AS TriggeredBy,
                trace_id AS TraceId,
                requested_at_utc AS RequestedAtUtc,
                location AS Location,
                salary_range AS SalaryRange,
                tags_json AS TagsJson,
                experience_level AS ExperienceLevel,
                job_type AS JobType,
                proxy_urls_json AS ProxyUrlsJson,
                max_pages AS MaxPages,
                status AS Status,
                attempt_count AS AttemptCount,
                next_attempt_at_utc AS NextAttemptAtUtc,
                error_message AS ErrorMessage
            FROM crawl_orchestration_queue_requests;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var items = await connection.QueryAsync<QueuedCrawlRequestState>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return items.ToArray();
    }

    public async Task<QueuedCrawlRequestState?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                request_id AS RequestId,
                crawl_run_id AS CrawlRunId,
                source AS Source,
                keyword AS Keyword,
                triggered_by AS TriggeredBy,
                trace_id AS TraceId,
                requested_at_utc AS RequestedAtUtc,
                location AS Location,
                salary_range AS SalaryRange,
                tags_json AS TagsJson,
                experience_level AS ExperienceLevel,
                job_type AS JobType,
                proxy_urls_json AS ProxyUrlsJson,
                max_pages AS MaxPages,
                status AS Status,
                attempt_count AS AttemptCount,
                next_attempt_at_utc AS NextAttemptAtUtc,
                error_message AS ErrorMessage
            FROM crawl_orchestration_queue_requests
            WHERE request_id = @RequestId
            LIMIT 1;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<QueuedCrawlRequestState>(
            new CommandDefinition(sql, new { RequestId = requestId }, cancellationToken: cancellationToken));
    }

    public async Task<QueuedCrawlRequestState?> TryAcquireNextAsync(CancellationToken cancellationToken)
    {
        const string selectSql = """
            SELECT
                request_id AS RequestId,
                crawl_run_id AS CrawlRunId,
                source AS Source,
                keyword AS Keyword,
                triggered_by AS TriggeredBy,
                trace_id AS TraceId,
                requested_at_utc AS RequestedAtUtc,
                location AS Location,
                salary_range AS SalaryRange,
                tags_json AS TagsJson,
                experience_level AS ExperienceLevel,
                job_type AS JobType,
                proxy_urls_json AS ProxyUrlsJson,
                max_pages AS MaxPages,
                status AS Status,
                attempt_count AS AttemptCount,
                next_attempt_at_utc AS NextAttemptAtUtc,
                error_message AS ErrorMessage
            FROM crawl_orchestration_queue_requests
            WHERE status = @PendingStatus
              AND next_attempt_at_utc <= @UtcNow
            ORDER BY requested_at_utc
            LIMIT 1
            FOR UPDATE;
            """;

        const string updateSql = """
            UPDATE crawl_orchestration_queue_requests
            SET status = @ProcessingStatus
            WHERE request_id = @RequestId;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await connection.QuerySingleOrDefaultAsync<QueuedCrawlRequestState>(
            new CommandDefinition(
                selectSql,
                new
                {
                    PendingStatus = QueueRequestStatus.Pending,
                    UtcNow = DateTimeOffset.UtcNow
                },
                transaction,
                cancellationToken: cancellationToken));

        if (current is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var acquired = current with { Status = QueueRequestStatus.Processing };
        await connection.ExecuteAsync(new CommandDefinition(
            updateSql,
            new
            {
                ProcessingStatus = QueueRequestStatus.Processing,
                current.RequestId
            },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return acquired;
    }

    public async Task ReturnToPendingAsync(
        Guid requestId,
        string? message,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE crawl_orchestration_queue_requests
            SET
                status = @Status,
                error_message = @ErrorMessage,
                next_attempt_at_utc = @NextAttemptAtUtc
            WHERE request_id = @RequestId;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                RequestId = requestId,
                Status = QueueRequestStatus.Pending,
                ErrorMessage = message,
                NextAttemptAtUtc = DateTimeOffset.UtcNow.Add(delay)
            },
            cancellationToken: cancellationToken));
    }

    public async Task AttachRunAsync(Guid requestId, Guid crawlRunId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE crawl_orchestration_queue_requests
            SET crawl_run_id = @CrawlRunId
            WHERE request_id = @RequestId;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { RequestId = requestId, CrawlRunId = crawlRunId },
            cancellationToken: cancellationToken));
    }

    public async Task MarkCompletedAsync(Guid requestId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE crawl_orchestration_queue_requests
            SET
                status = @CompletedStatus,
                attempt_count = attempt_count + 1,
                error_message = NULL
            WHERE request_id = @RequestId;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                RequestId = requestId,
                CompletedStatus = QueueRequestStatus.Completed
            },
            cancellationToken: cancellationToken));
    }

    public async Task MarkFailedOrRetryAsync(
        Guid requestId,
        string errorMessage,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var current = await GetByIdAsync(requestId, cancellationToken);
        if (current is null)
        {
            return;
        }

        var nextAttempt = current.AttemptCount + 1;
        var shouldRetry = nextAttempt < maxAttempts;

        const string sql = """
            UPDATE crawl_orchestration_queue_requests
            SET
                status = @Status,
                attempt_count = @AttemptCount,
                error_message = @ErrorMessage,
                next_attempt_at_utc = @NextAttemptAtUtc
            WHERE request_id = @RequestId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                RequestId = requestId,
                Status = shouldRetry ? QueueRequestStatus.Pending : QueueRequestStatus.Failed,
                AttemptCount = nextAttempt,
                ErrorMessage = errorMessage,
                NextAttemptAtUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Min(30, 5 * nextAttempt))
            },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> RetryFailedAsync(Guid requestId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE crawl_orchestration_queue_requests
            SET
                status = @PendingStatus,
                error_message = NULL,
                next_attempt_at_utc = @NextAttemptAtUtc,
                attempt_count = 0
            WHERE request_id = @RequestId
              AND status = @FailedStatus;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                RequestId = requestId,
                PendingStatus = QueueRequestStatus.Pending,
                FailedStatus = QueueRequestStatus.Failed,
                NextAttemptAtUtc = DateTimeOffset.UtcNow
            },
            cancellationToken: cancellationToken));
        if (affected == 0)
        {
            return false;
        }
        return true;
    }

    public async Task<QueueSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var items = await GetAllAsync(cancellationToken);
        var pending = items.Where(x => x.Status == QueueRequestStatus.Pending).ToArray();

        return new QueueSummary
        {
            PendingCount = pending.Length,
            ProcessingCount = items.Count(x => x.Status == QueueRequestStatus.Processing),
            CompletedCount = items.Count(x => x.Status == QueueRequestStatus.Completed),
            FailedCount = items.Count(x => x.Status == QueueRequestStatus.Failed),
            TotalCount = items.Count,
            OldestPendingRequestedAtUtc = pending.OrderBy(x => x.RequestedAtUtc).FirstOrDefault()?.RequestedAtUtc
        };
    }
}
