using Dapper;
using FJob.AdminOperationsService.Contracts;

namespace FJob.AdminOperationsService.Services;

public sealed class AdminAuditLogService(MySqlConnectionFactory connectionFactory)
{
    public async Task AppendAsync(
        string action,
        string target,
        string actor,
        bool success,
        string details,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO admin_operations_audit_logs (
                id,
                action,
                target,
                actor,
                success,
                details,
                occurred_at_utc
            )
            VALUES (
                @Id,
                @Action,
                @Target,
                @Actor,
                @Success,
                @Details,
                @OccurredAtUtc
            );
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                Id = Guid.NewGuid(),
                Action = action,
                Target = target,
                Actor = actor,
                Success = success,
                Details = details,
                OccurredAtUtc = DateTimeOffset.UtcNow
            },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyCollection<AdminAuditLogEntry>> GetRecentAsync(int limit, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                action AS Action,
                target AS Target,
                actor AS Actor,
                success AS Success,
                details AS Details,
                occurred_at_utc AS OccurredAtUtc
            FROM admin_operations_audit_logs
            ORDER BY occurred_at_utc DESC
            LIMIT @Limit;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var items = await connection.QueryAsync<AdminAuditLogEntry>(new CommandDefinition(
            sql,
            new { Limit = Math.Max(1, limit) },
            cancellationToken: cancellationToken));
        return items.ToArray();
    }
}
