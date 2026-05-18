using Dapper;

namespace FJob.JobSearchService.Services;

public sealed class DatabaseInitializer(MySqlConnectionFactory connectionFactory)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS job_search_documents (
                id CHAR(36) NOT NULL PRIMARY KEY,
                title VARCHAR(500) NOT NULL,
                normalized_title VARCHAR(500) NOT NULL,
                company VARCHAR(255) NOT NULL,
                normalized_company VARCHAR(255) NOT NULL,
                source VARCHAR(100) NOT NULL,
                url VARCHAR(1000) NOT NULL,
                location VARCHAR(255) NOT NULL,
                normalized_location VARCHAR(255) NOT NULL,
                salary VARCHAR(255) NOT NULL,
                salary_min_millions DECIMAL(10,2) NULL,
                salary_max_millions DECIMAL(10,2) NULL,
                description LONGTEXT NOT NULL,
                normalized_description LONGTEXT NOT NULL,
                tags_json JSON NOT NULL,
                posted_at_utc DATETIME(6) NOT NULL,
                updated_at_utc DATETIME(6) NOT NULL,
                KEY ix_job_search_updated_at (updated_at_utc),
                KEY ix_job_search_source (source)
            );

            CREATE TABLE IF NOT EXISTS job_search_sync_state (
                sync_key VARCHAR(50) NOT NULL PRIMARY KEY,
                last_successful_sync_utc DATETIME(6) NULL,
                last_attempted_sync_utc DATETIME(6) NULL,
                last_indexed_job_updated_at_utc DATETIME(6) NULL,
                last_indexed_count INT NOT NULL,
                last_error TEXT NULL
            );
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }
}
