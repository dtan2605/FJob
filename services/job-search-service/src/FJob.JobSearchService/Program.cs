using FJob.Contracts.Search;
using FJob.Observability;
using FJob.JobSearchService.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = ApplicationPathResolver.ResolveContentRoot("..", "..", "..")
});
builder.Configuration
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true, reloadOnChange: false)
    .AddJsonFile(
        Path.Combine(AppContext.BaseDirectory, $"appsettings.{builder.Environment.EnvironmentName}.json"),
        optional: true,
        reloadOnChange: false)
    .AddEnvironmentVariables();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddProblemDetails();
builder.Services.AddFJobObservability();
builder.Services.Configure<SearchOptions>(builder.Configuration.GetSection(SearchOptions.SectionName));

builder.Services.AddSingleton<IDistributedCache, NullDistributedCache>();
var redisConnectionString = builder.Configuration.GetSection(SearchOptions.SectionName)["RedisConnectionString"];
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
    });
}

builder.Services.AddHttpClient();
builder.Services.AddSingleton<MySqlConnectionFactory>();
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddSingleton<SearchDocumentMapper>();
builder.Services.AddSingleton<ISearchIndexStore, SearchIndexRepository>();
builder.Services.AddSingleton<SearchCacheService>();
builder.Services.AddSingleton<SearchQueryService>();
builder.Services.AddSingleton<SearchSyncCoordinator>();
builder.Services.AddHttpClient<JobCatalogExportClient>();
builder.Services.AddHostedService<JobCatalogSyncWorker>();

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILogger<Program>>();
await ResilienceExecutor.ExecuteAsync(
    "database-initialization",
    logger,
    ct => app.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync(ct),
    app.Lifetime.ApplicationStopping,
    maxAttempts: 5,
    timeoutSeconds: 10);

app.UseExceptionHandler();
app.UseFJobObservability();

app.MapGet("/health", async (
    ISearchIndexStore store,
    CancellationToken cancellationToken) =>
{
    var syncState = await store.GetSyncStateAsync(cancellationToken);
    var index = await store.GetAllAsync(cancellationToken);
    return Results.Ok(new
    {
        service = "job-search-service",
        status = "healthy",
        indexedDocuments = index.Count,
        lastSuccessfulSyncUtc = syncState.LastSuccessfulSyncUtc,
        lastAttemptedSyncUtc = syncState.LastAttemptedSyncUtc,
        lastIndexedJobUpdatedAtUtc = syncState.LastIndexedJobUpdatedAtUtc,
        lastIndexedCount = syncState.LastIndexedCount,
        lastError = syncState.LastError,
        time = DateTimeOffset.UtcNow
    });
});

app.MapGet("/metrics", async (
    ServiceMetrics serviceMetrics,
    ISearchIndexStore store,
    CancellationToken cancellationToken) =>
{
    var syncState = await store.GetSyncStateAsync(cancellationToken);
    var index = await store.GetAllAsync(cancellationToken);
    return Results.Ok(new
    {
        service = "job-search-service",
        metrics = serviceMetrics.Snapshot(),
        indexedDocuments = index.Count,
        syncState
    });
});

app.MapGet("/ready", async (
    MySqlConnectionFactory connectionFactory,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var jobCatalogBaseUrl = configuration.GetSection(SearchOptions.SectionName)["JobCatalogBaseUrl"] ?? "http://localhost:5101";
    var httpClient = httpClientFactory.CreateClient();

    try
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        await command.ExecuteScalarAsync(cancellationToken);

        using var response = await httpClient.GetAsync(new Uri(new Uri(jobCatalogBaseUrl), "/health"), cancellationToken);
        response.EnsureSuccessStatusCode();

        return Results.Ok(new
        {
            service = "job-search-service",
            ready = true,
            dependencies = new
            {
                mysql = "reachable",
                jobCatalog = "reachable"
            },
            time = DateTimeOffset.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            service = "job-search-service",
            ready = false,
            error = ex.Message,
            time = DateTimeOffset.UtcNow
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/api/search/jobs", async (
    SearchQueryRequest request,
    SearchQueryService queryService,
    CancellationToken cancellationToken) =>
{
    var response = await queryService.SearchAsync(request, cancellationToken);
    return Results.Ok(response);
});

app.MapGet("/api/search/index", async (
    ISearchIndexStore store,
    CancellationToken cancellationToken) =>
{
    var items = await store.GetAllAsync(cancellationToken);
    return Results.Ok(items.OrderByDescending(x => x.UpdatedAtUtc));
});

app.MapGet("/api/search/sync-state", async (
    ISearchIndexStore store,
    CancellationToken cancellationToken) =>
{
    var state = await store.GetSyncStateAsync(cancellationToken);
    return Results.Ok(state);
});

app.MapPost("/api/search/admin/sync", async (
    SearchSyncCoordinator coordinator,
    CancellationToken cancellationToken) =>
{
    var result = await coordinator.RunSyncAsync(fullRebuild: false, cancellationToken);
    return result.Success ? Results.Ok(result) : Results.Problem(result.ErrorMessage);
});

app.MapPost("/api/search/admin/rebuild", async (
    SearchSyncCoordinator coordinator,
    CancellationToken cancellationToken) =>
{
    var result = await coordinator.RunSyncAsync(fullRebuild: true, cancellationToken);
    return result.Success ? Results.Ok(result) : Results.Problem(result.ErrorMessage);
});

app.Run();
