using Dapper;
using FJob.Contracts.Operations;
using Microsoft.Extensions.Options;

namespace FJob.CrawlOrchestrationService.Services;

public sealed class SourceControlRepository(
    MySqlConnectionFactory connectionFactory,
    IOptions<OrchestrationOptions> options)
{
    public async Task<IReadOnlyCollection<SourceControlState>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        foreach (var source in options.Value.KnownSources)
        {
            const string ensureSql = """
                INSERT IGNORE INTO crawl_orchestration_source_controls (
                    source,
                    is_paused,
                    reason,
                    updated_at_utc
                )
                VALUES (@Source, @IsPaused, @Reason, @UpdatedAtUtc);
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                ensureSql,
                new
                {
                    Source = source,
                    IsPaused = false,
                    Reason = (string?)null,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                },
                cancellationToken: cancellationToken));
        }

        const string sql = """
            SELECT
                source AS Source,
                is_paused AS IsPaused,
                reason AS Reason,
                updated_at_utc AS UpdatedAtUtc
            FROM crawl_orchestration_source_controls
            ORDER BY source;
            """;

        var items = await connection.QueryAsync<SourceControlState>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return items.ToArray();
    }

    public async Task<SourceControlState> PauseAsync(string source, string? reason, CancellationToken cancellationToken)
    {
        return await UpsertAsync(source, true, reason, cancellationToken);
    }

    public async Task<SourceControlState> ResumeAsync(string source, CancellationToken cancellationToken)
    {
        return await UpsertAsync(source, false, null, cancellationToken);
    }

    public async Task<bool> IsPausedAsync(string source, CancellationToken cancellationToken)
    {
        var items = await GetAllAsync(cancellationToken);
        return items.FirstOrDefault(x => x.Source.Equals(source, StringComparison.OrdinalIgnoreCase))?.IsPaused ?? false;
    }

    private async Task<SourceControlState> UpsertAsync(
        string source,
        bool paused,
        string? reason,
        CancellationToken cancellationToken)
    {
        var value = new SourceControlState
        {
            Source = source,
            IsPaused = paused,
            Reason = paused ? reason : null,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        const string sql = """
            INSERT INTO crawl_orchestration_source_controls (
                source,
                is_paused,
                reason,
                updated_at_utc
            )
            VALUES (
                @Source,
                @IsPaused,
                @Reason,
                @UpdatedAtUtc
            )
            ON DUPLICATE KEY UPDATE
                is_paused = VALUES(is_paused),
                reason = VALUES(reason),
                updated_at_utc = VALUES(updated_at_utc);
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, value, cancellationToken: cancellationToken));
        return value;
    }
}
