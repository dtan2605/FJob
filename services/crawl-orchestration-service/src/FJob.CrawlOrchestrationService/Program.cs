using System.Net.Http.Json;
using FJob.Contracts.Crawl;
using FJob.CrawlOrchestrationService.Contracts;
using FJob.CrawlOrchestrationService;
using FJob.CrawlOrchestrationService.Services;
using FJob.Observability;

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
builder.Services.Configure<OrchestrationOptions>(builder.Configuration.GetSection(OrchestrationOptions.SectionName));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<MySqlConnectionFactory>();
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddSingleton<CrawlRequestQueue>();
builder.Services.AddSingleton<SourceControlRepository>();
builder.Services.AddHttpClient<JobCatalogClient>();
builder.Services.AddSingleton<PythonCrawlerExecutor>();
builder.Services.AddHostedService<RabbitMqCrawlRequestConsumer>();
builder.Services.AddHostedService<CrawlDispatcherWorker>();

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

app.MapGet("/health", () => Results.Ok(new
{
    service = "crawl-orchestration-service",
    status = "healthy",
    time = DateTimeOffset.UtcNow
}));

app.MapGet("/metrics", async (
    ServiceMetrics serviceMetrics,
    CrawlRequestQueue queue,
    SourceControlRepository sourceControlRepository,
    CancellationToken cancellationToken) =>
{
    var queueSummary = await queue.GetSummaryAsync(cancellationToken);
    var sourceStates = await sourceControlRepository.GetAllAsync(cancellationToken);
    return Results.Ok(new
    {
        service = "crawl-orchestration-service",
        metrics = serviceMetrics.Snapshot(),
        queueSummary,
        pausedSources = sourceStates.Count(x => x.IsPaused)
    });
});

app.MapGet("/ready", async (
    MySqlConnectionFactory connectionFactory,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var jobCatalogBaseUrl = configuration.GetSection(OrchestrationOptions.SectionName)["JobCatalogBaseUrl"] ?? "http://localhost:5101";
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
            service = "crawl-orchestration-service",
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
            service = "crawl-orchestration-service",
            ready = false,
            error = ex.Message,
            time = DateTimeOffset.UtcNow
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/api/crawl-requests", async (
    EnqueueCrawlRequest request,
    CrawlRequestQueue queue,
    SourceControlRepository sourceControls,
    CancellationToken cancellationToken) =>
{
    if (await sourceControls.IsPausedAsync(request.Source, cancellationToken))
    {
        return Results.Conflict(new
        {
            message = $"Source {request.Source} is paused and cannot accept new crawl requests."
        });
    }

    var message = new CrawlRequestMessage
    {
        RequestId = Guid.NewGuid(),
        Source = request.Source,
        Keyword = request.Keyword,
        TriggeredBy = string.IsNullOrWhiteSpace(request.TriggeredBy) ? "manual" : request.TriggeredBy,
        RequestedAtUtc = DateTimeOffset.UtcNow,
        TraceId = string.IsNullOrWhiteSpace(request.TraceId) ? Guid.NewGuid().ToString("N") : request.TraceId,
        Location = request.Location,
        SalaryRange = request.SalaryRange,
        Tags = request.Tags,
        ExperienceLevel = request.ExperienceLevel,
        JobType = request.JobType,
        ProxyUrls = request.ProxyUrls,
        MaxPages = request.MaxPages
    };

    var state = await queue.EnqueueAsync(message, cancellationToken);
    return Results.Accepted($"/api/crawl-requests/{state.RequestId}", state);
});

app.MapGet("/api/crawl-requests", async (
    CrawlRequestQueue queue,
    CancellationToken cancellationToken) =>
{
    var items = await queue.GetAllAsync(cancellationToken);
    return Results.Ok(items.OrderByDescending(x => x.RequestedAtUtc));
});

app.MapGet("/api/crawl-requests/{id:guid}", async (
    Guid id,
    CrawlRequestQueue queue,
    CancellationToken cancellationToken) =>
{
    var item = await queue.GetByIdAsync(id, cancellationToken);
    return item is null ? Results.NotFound() : Results.Ok(item);
});

app.MapGet("/api/operations/queue-summary", async (
    CrawlRequestQueue queue,
    CancellationToken cancellationToken) =>
{
    var summary = await queue.GetSummaryAsync(cancellationToken);
    return Results.Ok(summary);
});

app.MapPost("/api/operations/crawl-requests/{id:guid}/retry", async (
    Guid id,
    CrawlRequestQueue queue,
    CancellationToken cancellationToken) =>
{
    var retried = await queue.RetryFailedAsync(id, cancellationToken);
    return retried ? Results.Ok(new { message = "Retry queued." }) : Results.NotFound();
});

app.MapGet("/api/operations/sources", async (
    SourceControlRepository repository,
    CancellationToken cancellationToken) =>
{
    var items = await repository.GetAllAsync(cancellationToken);
    return Results.Ok(items);
});

app.MapPost("/api/operations/sources/{source}/pause", async (
    string source,
    UpdateSourceStateRequest request,
    SourceControlRepository repository,
    CancellationToken cancellationToken) =>
{
    var item = await repository.PauseAsync(source, request.Reason, cancellationToken);
    return Results.Ok(item);
});

app.MapPost("/api/operations/sources/{source}/resume", async (
    string source,
    SourceControlRepository repository,
    CancellationToken cancellationToken) =>
{
    var item = await repository.ResumeAsync(source, cancellationToken);
    return Results.Ok(item);
});

app.Run();
