using System.Text.Json;
using Dapper;
using FJob.Contracts.Jobs;

namespace FJob.JobCatalogService.Services;

public sealed class JobRepository(MySqlConnectionFactory connectionFactory)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyCollection<JobRecord>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                source_job_id AS SourceJobId,
                title AS Title,
                company AS Company,
                source AS Source,
                url AS Url,
                location AS Location,
                salary AS Salary,
                description AS Description,
                tags_json AS TagsJson,
                posted_at_utc AS PostedAtUtc,
                created_at_utc AS CreatedAtUtc,
                updated_at_utc AS UpdatedAtUtc
            FROM job_catalog_jobs
            ORDER BY updated_at_utc DESC;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<JobRecordRow>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.Select(Map).ToArray();
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM job_catalog_jobs;";
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<UpsertJobsResult> UpsertBatchAsync(
        UpsertJobsCommand command,
        TaggingService taggingService,
        CancellationToken cancellationToken)
    {
        var inserted = 0;
        var updated = 0;
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        foreach (var crawled in command.Jobs)
        {
            var canonicalTags = taggingService.MergeAndNormalize(crawled.Title, crawled.Description, crawled.Tags);
            const string getExistingSql = """
                SELECT
                    id AS Id,
                    source_job_id AS SourceJobId,
                    title AS Title,
                    company AS Company,
                    source AS Source,
                    url AS Url,
                    location AS Location,
                    salary AS Salary,
                    description AS Description,
                    tags_json AS TagsJson,
                    posted_at_utc AS PostedAtUtc,
                    created_at_utc AS CreatedAtUtc,
                    updated_at_utc AS UpdatedAtUtc
                FROM job_catalog_jobs
                WHERE source = @Source AND source_job_id = @SourceJobId
                LIMIT 1;
                """;

            var existing = await connection.QuerySingleOrDefaultAsync<JobRecordRow>(
                new CommandDefinition(
                    getExistingSql,
                    new { crawled.Source, crawled.SourceJobId },
                    cancellationToken: cancellationToken));

            if (existing is not null)
            {
                const string updateSql = """
                    UPDATE job_catalog_jobs
                    SET
                        title = @Title,
                        company = @Company,
                        source = @Source,
                        url = @Url,
                        location = @Location,
                        salary = @Salary,
                        description = @Description,
                        tags_json = @TagsJson,
                        posted_at_utc = @PostedAtUtc,
                        updated_at_utc = @UpdatedAtUtc
                    WHERE id = @Id;
                    """;

                await connection.ExecuteAsync(new CommandDefinition(
                    updateSql,
                    new
                    {
                        existing.Id,
                        crawled.Title,
                        crawled.Company,
                        crawled.Source,
                        crawled.Url,
                        crawled.Location,
                        crawled.Salary,
                        crawled.Description,
                        TagsJson = JsonSerializer.Serialize(canonicalTags, SerializerOptions),
                        crawled.PostedAtUtc,
                        UpdatedAtUtc = DateTimeOffset.UtcNow
                    },
                    cancellationToken: cancellationToken));
                updated++;
            }
            else
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
                    )
                    VALUES (
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
                        crawled.SourceJobId,
                        crawled.Title,
                        crawled.Company,
                        crawled.Source,
                        crawled.Url,
                        crawled.Location,
                        crawled.Salary,
                        crawled.Description,
                        TagsJson = JsonSerializer.Serialize(canonicalTags, SerializerOptions),
                        crawled.PostedAtUtc,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    },
                    cancellationToken: cancellationToken));
                inserted++;
            }
        }

        return new UpsertJobsResult
        {
            ImportedCount = inserted + updated,
            InsertedCount = inserted,
            UpdatedCount = updated
        };
    }

    private static JobRecord Map(JobRecordRow row)
    {
        return new JobRecord
        {
            Id = row.Id,
            SourceJobId = row.SourceJobId,
            Title = row.Title,
            Company = row.Company,
            Source = row.Source,
            Url = row.Url,
            Location = row.Location,
            Salary = row.Salary,
            Description = row.Description,
            Tags = string.IsNullOrWhiteSpace(row.TagsJson)
                ? []
                : JsonSerializer.Deserialize<string[]>(row.TagsJson, SerializerOptions) ?? [],
            PostedAtUtc = row.PostedAtUtc,
            CreatedAtUtc = row.CreatedAtUtc,
            UpdatedAtUtc = row.UpdatedAtUtc
        };
    }

    private sealed class JobRecordRow
    {
        public Guid Id { get; init; }
        public string SourceJobId { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Company { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public string Location { get; init; } = string.Empty;
        public string Salary { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string TagsJson { get; init; } = "[]";
        public DateTimeOffset PostedAtUtc { get; init; }
        public DateTimeOffset CreatedAtUtc { get; init; }
        public DateTimeOffset UpdatedAtUtc { get; init; }
    }
}
