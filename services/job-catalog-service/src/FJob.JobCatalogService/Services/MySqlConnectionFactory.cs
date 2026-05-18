using MySqlConnector;

namespace FJob.JobCatalogService.Services;

public sealed class MySqlConnectionFactory(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("MySql")
        ?? throw new InvalidOperationException("ConnectionStrings:MySql is not configured.");

    public async Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
