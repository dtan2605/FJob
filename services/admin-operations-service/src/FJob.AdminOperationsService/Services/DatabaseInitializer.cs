using Dapper;

namespace FJob.AdminOperationsService.Services;

public sealed class DatabaseInitializer(MySqlConnectionFactory connectionFactory)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS admin_operations_audit_logs (
                id CHAR(36) NOT NULL PRIMARY KEY,
                action VARCHAR(100) NOT NULL,
                target VARCHAR(255) NOT NULL,
                actor VARCHAR(100) NOT NULL,
                success BIT NOT NULL,
                details TEXT NOT NULL,
                occurred_at_utc DATETIME(6) NOT NULL,
                KEY ix_admin_operations_audit_logs_occurred_at (occurred_at_utc)
            );
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }
}
