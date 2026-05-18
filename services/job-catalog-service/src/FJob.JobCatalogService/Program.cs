using FJob.Contracts.Crawl;
using FJob.Contracts.Jobs;
using FJob.Observability;
using FJob.JobCatalogService.Services;

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
builder.Services.AddSingleton<MySqlConnectionFactory>();
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddSingleton<JobRepository>();
builder.Services.AddSingleton<CrawlRunRepository>();
builder.Services.AddSingleton<TaggingService>();

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
    service = "job-catalog-service",
    status = "healthy",
    time = DateTimeOffset.UtcNow
}));

app.MapGet("/metrics", async (
    ServiceMetrics serviceMetrics,
    JobRepository jobRepository,
    CrawlRunRepository crawlRunRepository,
    CancellationToken cancellationToken) =>
{
    var jobs = await jobRepository.GetAllAsync(cancellationToken);
    var runs = await crawlRunRepository.GetAllAsync(cancellationToken);
    return Results.Ok(new
    {
        service = "job-catalog-service",
        metrics = serviceMetrics.Snapshot(),
        jobsCount = jobs.Count,
        crawlRunsCount = runs.Count
    });
});

app.MapGet("/ready", async (
    MySqlConnectionFactory connectionFactory,
    CancellationToken cancellationToken) =>
{
    try
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        await command.ExecuteScalarAsync(cancellationToken);

        return Results.Ok(new
        {
            service = "job-catalog-service",
            ready = true,
            dependencies = new
            {
                mysql = "reachable"
            },
            time = DateTimeOffset.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            service = "job-catalog-service",
            ready = false,
            dependencies = new
            {
                mysql = "unreachable"
            },
            error = ex.Message,
            time = DateTimeOffset.UtcNow
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/api/crawl-runs", async (
    CreateCrawlRunCommand command,
    CrawlRunRepository repository,
    CancellationToken cancellationToken) =>
{
    var run = new CrawlRunSummary
    {
        Id = Guid.NewGuid(),
        Source = command.Source,
        Keyword = command.Keyword,
        TriggeredBy = command.TriggeredBy,
        Status = CrawlRunStatus.Requested,
        TraceId = command.TraceId,
        RequestedAtUtc = command.RequestedAtUtc
    };

    await repository.CreateAsync(run, cancellationToken);
    return Results.Created($"/api/crawl-runs/{run.Id}", run);
});

app.MapPut("/api/crawl-runs/{id:guid}/status", async (
    Guid id,
    CrawlRunUpdateCommand command,
    CrawlRunRepository repository,
    CancellationToken cancellationToken) =>
{
    var updated = await repository.UpdateAsync(id, command, cancellationToken);
    return updated is null ? Results.NotFound() : Results.Ok(updated);
});

app.MapGet("/api/crawl-runs", async (
    CrawlRunRepository repository,
    CancellationToken cancellationToken) =>
{
    var runs = await repository.GetAllAsync(cancellationToken);
    return Results.Ok(runs.OrderByDescending(x => x.RequestedAtUtc));
});

app.MapGet("/api/jobs", async (
    JobRepository repository,
    CancellationToken cancellationToken) =>
{
    var jobs = await repository.GetAllAsync(cancellationToken);
    return Results.Ok(jobs.OrderByDescending(x => x.UpdatedAtUtc));
});

app.MapGet("/api/jobs/export", async (
    DateTimeOffset? updatedAfterUtc,
    JobRepository repository,
    CancellationToken cancellationToken) =>
{
    var jobs = await repository.GetAllAsync(cancellationToken);
    if (updatedAfterUtc.HasValue)
    {
        jobs = jobs.Where(job => job.UpdatedAtUtc > updatedAfterUtc.Value).ToArray();
    }

    return Results.Ok(jobs.OrderBy(x => x.UpdatedAtUtc));
});

app.MapPost("/api/jobs/upsert-batch", async (
    UpsertJobsCommand command,
    JobRepository repository,
    TaggingService taggingService,
    CancellationToken cancellationToken) =>
{
    var result = await repository.UpsertBatchAsync(command, taggingService, cancellationToken);
    return Results.Ok(result);
});

app.MapPost("/api/admin/jobs/clear", async (
    JobRepository repository,
    CancellationToken cancellationToken) =>
{
    await repository.ClearAsync(cancellationToken);
    return Results.NoContent();
});

app.Run();
