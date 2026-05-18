using Dapper;
using FJob.IdentityAccessService.Models;
using Microsoft.Extensions.Options;

namespace FJob.IdentityAccessService.Services;

public sealed class IdentityRepository(
    MySqlConnectionFactory connectionFactory,
    IOptions<IdentityOptions> identityOptions)
{
    public async Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM identity_access_users
            WHERE username = @Username;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(sql, new { Username = username }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        foreach (var seedUser in identityOptions.Value.SeedUsers)
        {
            const string existsSql = """
                SELECT COUNT(1)
                FROM identity_access_users
                WHERE username = @Username;
                """;

            var exists = await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(existsSql, new { seedUser.Username }, cancellationToken: cancellationToken)) > 0;

            if (exists)
            {
                continue;
            }

            const string insertSql = """
                INSERT INTO identity_access_users (
                    id,
                    username,
                    password_hash,
                    role,
                    is_active,
                    created_at_utc,
                    updated_at_utc
                )
                VALUES (
                    @Id,
                    @Username,
                    @PasswordHash,
                    @Role,
                    @IsActive,
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
                    seedUser.Username,
                    PasswordHash = PasswordHasher.Hash(seedUser.Password),
                    seedUser.Role,
                    IsActive = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                },
                cancellationToken: cancellationToken));
        }
    }

    public async Task<IdentityUserRecord?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                username AS Username,
                password_hash AS PasswordHash,
                role AS Role,
                is_active AS IsActive,
                created_at_utc AS CreatedAtUtc,
                updated_at_utc AS UpdatedAtUtc
            FROM identity_access_users
            WHERE username = @Username
            LIMIT 1;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<IdentityUserRecord>(
            new CommandDefinition(sql, new { Username = username }, cancellationToken: cancellationToken));
    }

    public async Task<IdentityUserRecord> CreateUserAsync(
        string username,
        string password,
        string role,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO identity_access_users (
                id,
                username,
                password_hash,
                role,
                is_active,
                created_at_utc,
                updated_at_utc
            )
            VALUES (
                @Id,
                @Username,
                @PasswordHash,
                @Role,
                @IsActive,
                @CreatedAtUtc,
                @UpdatedAtUtc
            );
            """;

        var now = DateTimeOffset.UtcNow;
        var user = new IdentityUserRecord
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = PasswordHasher.Hash(password),
            Role = role,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                user.Id,
                user.Username,
                user.PasswordHash,
                user.Role,
                user.IsActive,
                user.CreatedAtUtc,
                user.UpdatedAtUtc
            },
            cancellationToken: cancellationToken));

        return user;
    }

    public async Task AppendAuditAsync(
        string eventType,
        string username,
        bool success,
        string details,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO identity_access_audit_logs (
                id,
                event_type,
                username,
                success,
                details,
                occurred_at_utc
            )
            VALUES (
                @Id,
                @EventType,
                @Username,
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
                EventType = eventType,
                Username = username,
                Success = success,
                Details = details,
                OccurredAtUtc = DateTimeOffset.UtcNow
            },
            cancellationToken: cancellationToken));
    }
}
