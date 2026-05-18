using Dapper;

namespace FJob.CrawlOrchestrationService.Services;

public sealed class DatabaseInitializer(MySqlConnectionFactory connectionFactory)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        const string createTablesSql = """
            CREATE TABLE IF NOT EXISTS crawl_orchestration_queue_requests (
                request_id CHAR(36) NOT NULL PRIMARY KEY,
                crawl_run_id CHAR(36) NULL,
                source VARCHAR(100) NOT NULL,
                keyword VARCHAR(255) NOT NULL,
                triggered_by VARCHAR(100) NOT NULL,
                trace_id VARCHAR(100) NOT NULL,
                requested_at_utc DATETIME(6) NOT NULL,
                location VARCHAR(255) NULL,
                salary_range VARCHAR(100) NULL,
                tags_json TEXT NULL,
                experience_level VARCHAR(100) NULL,
                job_type VARCHAR(100) NULL,
                proxy_urls_json TEXT NULL,
                max_pages INT NULL,
                status INT NOT NULL,
                attempt_count INT NOT NULL,
                next_attempt_at_utc DATETIME(6) NOT NULL,
                error_message TEXT NULL,
                KEY ix_crawl_orchestration_queue_next_attempt (status, next_attempt_at_utc),
                KEY ix_crawl_orchestration_queue_requested_at (requested_at_utc)
            );

            CREATE TABLE IF NOT EXISTS crawl_orchestration_source_controls (
                source VARCHAR(100) NOT NULL PRIMARY KEY,
                is_paused BIT NOT NULL,
                reason TEXT NULL,
                updated_at_utc DATETIME(6) NOT NULL
            );
            """;

        const string columnExistsSql = """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name = 'crawl_orchestration_queue_requests'
              AND column_name = 'max_pages';
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(createTablesSql, cancellationToken: cancellationToken));

        var maxPagesColumnExists = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(columnExistsSql, cancellationToken: cancellationToken));

        if (maxPagesColumnExists == 0)
        {
            const string addMaxPagesColumnSql = """
                ALTER TABLE crawl_orchestration_queue_requests
                ADD COLUMN max_pages INT NULL AFTER proxy_urls_json;
                """;

            await connection.ExecuteAsync(new CommandDefinition(addMaxPagesColumnSql, cancellationToken: cancellationToken));
        }
    }
}
