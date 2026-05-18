using System.Text.Json;
using Dapper;
using FJob.Contracts.Jobs;
using FJob.Contracts.Search;

namespace FJob.JobSearchService.Services;

public sealed class SearchIndexRepository(
    MySqlConnectionFactory connectionFactory,
    SearchDocumentMapper mapper) : ISearchIndexStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private const string SyncKey = "default";

    public async Task<IReadOnlyCollection<SearchDocument>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                title AS Title,
                normalized_title AS NormalizedTitle,
                company AS Company,
                normalized_company AS NormalizedCompany,
                source AS Source,
                url AS Url,
                location AS Location,
                normalized_location AS NormalizedLocation,
                salary AS Salary,
                salary_min_millions AS SalaryMinMillions,
                salary_max_millions AS SalaryMaxMillions,
                description AS Description,
                normalized_description AS NormalizedDescription,
                tags_json AS TagsJson,
                posted_at_utc AS PostedAtUtc,
                updated_at_utc AS UpdatedAtUtc
            FROM job_search_documents;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<SearchDocumentRow>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.Select(Map).ToArray();
    }

    public async Task<SearchIndexSyncResult> UpsertBatchAsync(
        IReadOnlyCollection<JobRecord> jobs,
        CancellationToken cancellationToken)
    {
        var inserted = 0;
        var updated = 0;
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        foreach (var job in jobs)
        {
            var mapped = mapper.Map(job);
            const string existingSql = "SELECT COUNT(1) FROM job_search_documents WHERE id = @Id;";
            var exists = await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(existingSql, new { mapped.Id }, cancellationToken: cancellationToken)) > 0;

            const string upsertSql = """
                INSERT INTO job_search_documents (
                    id,
                    title,
                    normalized_title,
                    company,
                    normalized_company,
                    source,
                    url,
                    location,
                    normalized_location,
                    salary,
                    salary_min_millions,
                    salary_max_millions,
                    description,
                    normalized_description,
                    tags_json,
                    posted_at_utc,
                    updated_at_utc
                )
                VALUES (
                    @Id,
                    @Title,
                    @NormalizedTitle,
                    @Company,
                    @NormalizedCompany,
                    @Source,
                    @Url,
                    @Location,
                    @NormalizedLocation,
                    @Salary,
                    @SalaryMinMillions,
                    @SalaryMaxMillions,
                    @Description,
                    @NormalizedDescription,
                    @TagsJson,
                    @PostedAtUtc,
                    @UpdatedAtUtc
                )
                ON DUPLICATE KEY UPDATE
                    title = VALUES(title),
                    normalized_title = VALUES(normalized_title),
                    company = VALUES(company),
                    normalized_company = VALUES(normalized_company),
                    source = VALUES(source),
                    url = VALUES(url),
                    location = VALUES(location),
                    normalized_location = VALUES(normalized_location),
                    salary = VALUES(salary),
                    salary_min_millions = VALUES(salary_min_millions),
                    salary_max_millions = VALUES(salary_max_millions),
                    description = VALUES(description),
                    normalized_description = VALUES(normalized_description),
                    tags_json = VALUES(tags_json),
                    posted_at_utc = VALUES(posted_at_utc),
                    updated_at_utc = VALUES(updated_at_utc);
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                upsertSql,
                new
                {
                    mapped.Id,
                    mapped.Title,
                    mapped.NormalizedTitle,
                    mapped.Company,
                    mapped.NormalizedCompany,
                    mapped.Source,
                    mapped.Url,
                    mapped.Location,
                    mapped.NormalizedLocation,
                    mapped.Salary,
                    mapped.SalaryMinMillions,
                    mapped.SalaryMaxMillions,
                    mapped.Description,
                    mapped.NormalizedDescription,
                    TagsJson = JsonSerializer.Serialize(mapped.Tags, SerializerOptions),
                    mapped.PostedAtUtc,
                    mapped.UpdatedAtUtc
                },
                cancellationToken: cancellationToken));

            if (exists)
            {
                updated++;
            }
            else
            {
                inserted++;
            }
        }

        return new SearchIndexSyncResult
        {
            IndexedCount = inserted + updated,
            InsertedCount = inserted,
            UpdatedCount = updated
        };
    }

    public async Task<SyncState> GetSyncStateAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                last_successful_sync_utc AS LastSuccessfulSyncUtc,
                last_attempted_sync_utc AS LastAttemptedSyncUtc,
                last_indexed_job_updated_at_utc AS LastIndexedJobUpdatedAtUtc,
                last_indexed_count AS LastIndexedCount,
                last_error AS LastError
            FROM job_search_sync_state
            WHERE sync_key = @SyncKey
            LIMIT 1;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<SyncState>(
            new CommandDefinition(sql, new { SyncKey }, cancellationToken: cancellationToken))
            ?? new SyncState();
    }

    public async Task SaveSyncStateAsync(SyncState state, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO job_search_sync_state (
                sync_key,
                last_successful_sync_utc,
                last_attempted_sync_utc,
                last_indexed_job_updated_at_utc,
                last_indexed_count,
                last_error
            )
            VALUES (
                @SyncKey,
                @LastSuccessfulSyncUtc,
                @LastAttemptedSyncUtc,
                @LastIndexedJobUpdatedAtUtc,
                @LastIndexedCount,
                @LastError
            )
            ON DUPLICATE KEY UPDATE
                last_successful_sync_utc = VALUES(last_successful_sync_utc),
                last_attempted_sync_utc = VALUES(last_attempted_sync_utc),
                last_indexed_job_updated_at_utc = VALUES(last_indexed_job_updated_at_utc),
                last_indexed_count = VALUES(last_indexed_count),
                last_error = VALUES(last_error);
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                SyncKey,
                state.LastSuccessfulSyncUtc,
                state.LastAttemptedSyncUtc,
                state.LastIndexedJobUpdatedAtUtc,
                state.LastIndexedCount,
                state.LastError
            },
            cancellationToken: cancellationToken));
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM job_search_documents;";
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    private static SearchDocument Map(SearchDocumentRow row)
    {
        return new SearchDocument
        {
            Id = row.Id,
            Title = row.Title,
            NormalizedTitle = row.NormalizedTitle,
            Company = row.Company,
            NormalizedCompany = row.NormalizedCompany,
            Source = row.Source,
            Url = row.Url,
            Location = row.Location,
            NormalizedLocation = row.NormalizedLocation,
            Salary = row.Salary,
            SalaryMinMillions = row.SalaryMinMillions,
            SalaryMaxMillions = row.SalaryMaxMillions,
            Description = row.Description,
            NormalizedDescription = row.NormalizedDescription,
            Tags = string.IsNullOrWhiteSpace(row.TagsJson)
                ? []
                : JsonSerializer.Deserialize<string[]>(row.TagsJson, SerializerOptions) ?? [],
            PostedAtUtc = row.PostedAtUtc,
            UpdatedAtUtc = row.UpdatedAtUtc
        };
    }

    private sealed class SearchDocumentRow
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string NormalizedTitle { get; init; } = string.Empty;
        public string Company { get; init; } = string.Empty;
        public string NormalizedCompany { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public string Location { get; init; } = string.Empty;
        public string NormalizedLocation { get; init; } = string.Empty;
        public string Salary { get; init; } = string.Empty;
        public decimal? SalaryMinMillions { get; init; }
        public decimal? SalaryMaxMillions { get; init; }
        public string Description { get; init; } = string.Empty;
        public string NormalizedDescription { get; init; } = string.Empty;
        public string TagsJson { get; init; } = "[]";
        public DateTimeOffset PostedAtUtc { get; init; }
        public DateTimeOffset UpdatedAtUtc { get; init; }
    }
}
