using Dapper;

namespace FJob.IdentityAccessService.Services;

public sealed class DatabaseInitializer(
    MySqlConnectionFactory connectionFactory,
    IdentityRepository repository)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS identity_access_users (
                id CHAR(36) NOT NULL PRIMARY KEY,
                username VARCHAR(100) NOT NULL UNIQUE,
                password_hash VARCHAR(500) NOT NULL,
                role VARCHAR(50) NOT NULL,
                is_active BIT NOT NULL,
                created_at_utc DATETIME(6) NOT NULL,
                updated_at_utc DATETIME(6) NOT NULL
            );

            CREATE TABLE IF NOT EXISTS identity_access_audit_logs (
                id CHAR(36) NOT NULL PRIMARY KEY,
                event_type VARCHAR(100) NOT NULL,
                username VARCHAR(100) NOT NULL,
                success BIT NOT NULL,
                details TEXT NOT NULL,
                occurred_at_utc DATETIME(6) NOT NULL,
                KEY ix_identity_access_audit_logs_occurred_at (occurred_at_utc)
            );
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
        await repository.SeedAsync(cancellationToken);
    }
}
