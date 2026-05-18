using System.Text.Json;
using Dapper;

namespace FJob.JobCatalogService.Services;

public sealed class DatabaseInitializer(MySqlConnectionFactory connectionFactory)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS job_catalog_jobs (
                id CHAR(36) NOT NULL PRIMARY KEY,
                source_job_id VARCHAR(255) NOT NULL,
                title VARCHAR(500) NOT NULL,
                company VARCHAR(255) NOT NULL,
                source VARCHAR(100) NOT NULL,
                url VARCHAR(1000) NOT NULL,
                location VARCHAR(255) NOT NULL,
                salary VARCHAR(255) NOT NULL,
                description LONGTEXT NOT NULL,
                tags_json JSON NOT NULL,
                posted_at_utc DATETIME(6) NOT NULL,
                created_at_utc DATETIME(6) NOT NULL,
                updated_at_utc DATETIME(6) NOT NULL,
                UNIQUE KEY ux_job_catalog_jobs_source_job (source, source_job_id),
                KEY ix_job_catalog_jobs_updated_at (updated_at_utc)
            );

            CREATE TABLE IF NOT EXISTS job_catalog_crawl_runs (
                id CHAR(36) NOT NULL PRIMARY KEY,
                source VARCHAR(100) NOT NULL,
                keyword VARCHAR(255) NOT NULL,
                triggered_by VARCHAR(100) NOT NULL,
                status INT NOT NULL,
                attempt_count INT NOT NULL,
                imported_jobs INT NOT NULL,
                error_message TEXT NULL,
                trace_id VARCHAR(100) NOT NULL,
                requested_at_utc DATETIME(6) NOT NULL,
                started_at_utc DATETIME(6) NULL,
                finished_at_utc DATETIME(6) NULL,
                KEY ix_job_catalog_crawl_runs_requested_at (requested_at_utc),
                KEY ix_job_catalog_crawl_runs_status (status)
            );
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));

        const string countSql = "SELECT COUNT(*) FROM job_catalog_jobs;";
        var existingJobs = await connection.ExecuteScalarAsync<int>(new CommandDefinition(countSql, cancellationToken: cancellationToken));

        if (existingJobs == 0)
        {
            const string insertSql = """
                INSERT INTO job_catalog_jobs (
                    id,
                    source_job_id,
                    title,
                    company,
                    source,
                    url,
                    location,
                    salary,
                    description,
                    tags_json,
                    posted_at_utc,
                    created_at_utc,
                    updated_at_utc
                ) VALUES (
                    @Id,
                    @SourceJobId,
                    @Title,
                    @Company,
                    @Source,
                    @Url,
                    @Location,
                    @Salary,
                    @Description,
                    @TagsJson,
                    @PostedAtUtc,
                    @CreatedAtUtc,
                    @UpdatedAtUtc
                );
                """;

            var now = DateTimeOffset.UtcNow;
            await connection.ExecuteAsync(new CommandDefinition(
                insertSql,
                new
                {
                    Id = Guid.NewGuid(),
                    SourceJobId = "demo-1",
                    Title = "Demo Software Engineer",
                    Company = "FJob Example Co.",
                    Source = "demo",
                    Url = "https://example.com/jobs/demo-software-engineer",
                    Location = "Remote",
                    Salary = "Competitive",
                    Description = "This is a demo job inserted at startup so the search index has sample data.",
                    TagsJson = JsonSerializer.Serialize(new[] { "software", "engineer", "demo" }),
                    PostedAtUtc = now,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                },
                cancellationToken: cancellationToken));
        }
    }
}
