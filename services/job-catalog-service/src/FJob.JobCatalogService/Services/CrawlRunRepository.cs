using Dapper;
using FJob.Contracts.Crawl;

namespace FJob.JobCatalogService.Services;

public sealed class CrawlRunRepository(MySqlConnectionFactory connectionFactory)
{
    public async Task CreateAsync(CrawlRunSummary run, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO job_catalog_crawl_runs (
                id,
                source,
                keyword,
                triggered_by,
                status,
                attempt_count,
                imported_jobs,
                error_message,
                trace_id,
                requested_at_utc,
                started_at_utc,
                finished_at_utc
            )
            VALUES (
                @Id,
                @Source,
                @Keyword,
                @TriggeredBy,
                @Status,
                @AttemptCount,
                @ImportedJobs,
                @ErrorMessage,
                @TraceId,
                @RequestedAtUtc,
                @StartedAtUtc,
                @FinishedAtUtc
            );
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, run, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyCollection<CrawlRunSummary>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                source AS Source,
                keyword AS Keyword,
                triggered_by AS TriggeredBy,
                status AS Status,
                attempt_count AS AttemptCount,
                imported_jobs AS ImportedJobs,
                error_message AS ErrorMessage,
                trace_id AS TraceId,
                requested_at_utc AS RequestedAtUtc,
                started_at_utc AS StartedAtUtc,
                finished_at_utc AS FinishedAtUtc
            FROM job_catalog_crawl_runs
            ORDER BY requested_at_utc DESC;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var items = await connection.QueryAsync<CrawlRunSummary>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return items.ToArray();
    }

    public async Task<CrawlRunSummary?> UpdateAsync(
        Guid id,
        CrawlRunUpdateCommand command,
        CancellationToken cancellationToken)
    {
        const string getSql = """
            SELECT
                id AS Id,
                source AS Source,
                keyword AS Keyword,
                triggered_by AS TriggeredBy,
                status AS Status,
                attempt_count AS AttemptCount,
                imported_jobs AS ImportedJobs,
                error_message AS ErrorMessage,
                trace_id AS TraceId,
                requested_at_utc AS RequestedAtUtc,
                started_at_utc AS StartedAtUtc,
                finished_at_utc AS FinishedAtUtc
            FROM job_catalog_crawl_runs
            WHERE id = @Id
            LIMIT 1;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var current = await connection.QuerySingleOrDefaultAsync<CrawlRunSummary>(
            new CommandDefinition(getSql, new { Id = id }, cancellationToken: cancellationToken));

        if (current is null)
        {
            return null;
        }

        var updated = new CrawlRunSummary
        {
            Id = current.Id,
            Source = current.Source,
            Keyword = current.Keyword,
            TriggeredBy = current.TriggeredBy,
            Status = command.Status,
            AttemptCount = command.AttemptCount,
            ImportedJobs = command.ImportedJobs,
            ErrorMessage = command.ErrorMessage,
            TraceId = current.TraceId,
            RequestedAtUtc = current.RequestedAtUtc,
            StartedAtUtc = command.StartedAtUtc ?? current.StartedAtUtc,
            FinishedAtUtc = command.FinishedAtUtc ?? current.FinishedAtUtc
        };

        const string updateSql = """
            UPDATE job_catalog_crawl_runs
            SET
                status = @Status,
                attempt_count = @AttemptCount,
                imported_jobs = @ImportedJobs,
                error_message = @ErrorMessage,
                started_at_utc = @StartedAtUtc,
                finished_at_utc = @FinishedAtUtc
            WHERE id = @Id;
            """;

        await connection.ExecuteAsync(new CommandDefinition(updateSql, updated, cancellationToken: cancellationToken));
        return updated;
    }
}
