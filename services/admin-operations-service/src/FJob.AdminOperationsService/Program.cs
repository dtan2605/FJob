using System.Security.Claims;
using System.Text;
using FJob.AdminOperationsService;
using FJob.AdminOperationsService.Contracts;
using FJob.AdminOperationsService.Services;
using FJob.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = ApplicationPathResolver.ResolveContentRoot("..", "..", ".."),
    WebRootPath = "wwwroot"
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
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "App_Data", "DataProtectionKeys")))
    .SetApplicationName("FJob");
builder.Services.Configure<AdminOperationsOptions>(builder.Configuration.GetSection(AdminOperationsOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<MySqlConnectionFactory>();
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddHttpClient<OrchestrationOperationsClient>();
builder.Services.AddHttpClient<JobCatalogOperationsClient>();
builder.Services.AddHttpClient<IdentityAccessClient>();
builder.Services.AddSingleton<AdminAuditLogService>();
builder.Services.AddSingleton<OperationsDashboardService>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                if (context.Request.Path.StartsWithSegments("/api/admin") &&
                    !context.Request.Path.StartsWithSegments("/api/admin/auth"))
                {
                    var auditService = context.HttpContext.RequestServices.GetRequiredService<AdminAuditLogService>();
                    await auditService.AppendAsync(
                        "failed-access",
                        context.Request.Path,
                        context.HttpContext.User.Identity?.Name ?? "anonymous",
                        success: false,
                        details: "Unauthorized admin API access attempt.",
                        context.HttpContext.RequestAborted);
                }
            },
            OnForbidden = async context =>
            {
                var auditService = context.HttpContext.RequestServices.GetRequiredService<AdminAuditLogService>();
                await auditService.AppendAsync(
                    "failed-access",
                    context.Request.Path,
                    context.HttpContext.User.Identity?.Name ?? "anonymous",
                    success: false,
                    details: "Forbidden admin API access attempt.",
                    context.HttpContext.RequestAborted);
            }
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminReadonly", policy => policy.RequireRole("readonly", "operator"))
    .AddPolicy("AdminOperator", policy => policy.RequireRole("operator"));

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
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    service = "admin-operations-service",
    status = "healthy",
    time = DateTimeOffset.UtcNow
}));

app.MapGet("/metrics", (ServiceMetrics serviceMetrics) =>
{
    return Results.Ok(new
    {
        service = "admin-operations-service",
        metrics = serviceMetrics.Snapshot()
    });
}).RequireAuthorization("AdminReadonly");

app.MapGet("/ready", async (
    MySqlConnectionFactory connectionFactory,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var options = configuration.GetSection(AdminOperationsOptions.SectionName);
    var orchestrationBaseUrl = options["OrchestrationBaseUrl"] ?? "http://localhost:5102";
    var jobCatalogBaseUrl = options["JobCatalogBaseUrl"] ?? "http://localhost:5101";
    var identityBaseUrl = options["IdentityBaseUrl"] ?? "http://localhost:5104";
    var httpClient = httpClientFactory.CreateClient();

    try
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        await command.ExecuteScalarAsync(cancellationToken);

        using var orchestrationResponse = await httpClient.GetAsync(new Uri(new Uri(orchestrationBaseUrl), "/health"), cancellationToken);
        orchestrationResponse.EnsureSuccessStatusCode();

        using var jobCatalogResponse = await httpClient.GetAsync(new Uri(new Uri(jobCatalogBaseUrl), "/health"), cancellationToken);
        jobCatalogResponse.EnsureSuccessStatusCode();

        using var identityResponse = await httpClient.GetAsync(new Uri(new Uri(identityBaseUrl), "/health"), cancellationToken);
        identityResponse.EnsureSuccessStatusCode();

        return Results.Ok(new
        {
            service = "admin-operations-service",
            ready = true,
            dependencies = new
            {
                mysql = "reachable",
                orchestration = "reachable",
                jobCatalog = "reachable",
                identity = "reachable"
            },
            time = DateTimeOffset.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            service = "admin-operations-service",
            ready = false,
            error = ex.Message,
            time = DateTimeOffset.UtcNow
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapGet("/api/admin/config", (IConfiguration configuration) =>
{
    var gatewayBaseUrl = configuration.GetSection(AdminOperationsOptions.SectionName)["ApiGatewayBaseUrl"] ?? "http://localhost:5100";
    return Results.Ok(new { apiGatewayBaseUrl = gatewayBaseUrl });
});

app.MapPost("/api/admin/auth/login", async (
    AdminLoginRequest request,
    IdentityAccessClient client,
    CancellationToken cancellationToken) =>
{
    var result = await client.LoginAsync(request, cancellationToken);
    return result.Success ? Results.Ok(result.Data) : Results.Json(result, statusCode: result.StatusCode);
});

app.MapPost("/api/admin/auth/logout", async (
    HttpContext httpContext,
    IdentityAccessClient client,
    CancellationToken cancellationToken) =>
{
    var token = ExtractBearerToken(httpContext);
    if (string.IsNullOrWhiteSpace(token))
    {
        return Results.Unauthorized();
    }

    var success = await client.LogoutAsync(token, cancellationToken);
    return success ? Results.Ok(new { message = "Logout successful." }) : Results.Unauthorized();
}).RequireAuthorization("AdminReadonly");

app.MapGet("/api/admin/auth/me", async (
    HttpContext httpContext,
    IdentityAccessClient client,
    CancellationToken cancellationToken) =>
{
    var token = ExtractBearerToken(httpContext);
    if (string.IsNullOrWhiteSpace(token))
    {
        return Results.Unauthorized();
    }

    var user = await client.GetCurrentUserAsync(token, cancellationToken);
    return user is null ? Results.Unauthorized() : Results.Ok(user);
}).RequireAuthorization("AdminReadonly");

app.MapGet("/api/admin/dashboard", async (
    OperationsDashboardService dashboardService,
    CancellationToken cancellationToken) =>
{
    var snapshot = await dashboardService.GetDashboardAsync(cancellationToken);
    return Results.Ok(snapshot);
}).RequireAuthorization("AdminReadonly");

app.MapGet("/api/admin/crawl-runs", async (
    JobCatalogOperationsClient client,
    CancellationToken cancellationToken) =>
{
    var items = await client.GetCrawlRunsAsync(cancellationToken);
    return Results.Ok(items);
}).RequireAuthorization("AdminReadonly");

app.MapGet("/api/admin/queue", async (
    OrchestrationOperationsClient client,
    CancellationToken cancellationToken) =>
{
    var items = await client.GetQueueItemsAsync(cancellationToken);
    return Results.Ok(items);
}).RequireAuthorization("AdminReadonly");

app.MapGet("/api/admin/audit-logs", async (
    int? limit,
    AdminAuditLogService auditLogService,
    CancellationToken cancellationToken) =>
{
    var items = await auditLogService.GetRecentAsync(limit ?? 30, cancellationToken);
    return Results.Ok(items);
}).RequireAuthorization("AdminReadonly");

app.MapPost("/api/admin/crawl-requests", async (
    AdminManualCrawlRequest request,
    OrchestrationOperationsClient client,
    AdminAuditLogService auditLogService,
    CancellationToken cancellationToken) =>
{
    var result = await client.TriggerManualCrawlAsync(request, cancellationToken);
    await auditLogService.AppendAsync(
        action: "manual-crawl-trigger",
        target: request.Source,
        actor: string.IsNullOrWhiteSpace(request.TriggeredBy) ? "admin-dashboard" : request.TriggeredBy,
        success: result.Success,
        details: result.Success
            ? $"Queued crawl request for keyword '{request.Keyword}'."
            : $"Trigger failed for keyword '{request.Keyword}': {result.Message}",
        cancellationToken);
    return result.Success
        ? Results.Ok(result)
        : Results.Json(result, statusCode: result.StatusCode);
}).RequireAuthorization("AdminOperator");

app.MapPost("/api/admin/crawl-all-sources", async (
    AdminCrawlAllSourcesRequest request,
    OrchestrationOperationsClient client,
    AdminAuditLogService auditLogService,
    CancellationToken cancellationToken) =>
{
    var sources = await client.GetSourcesAsync(cancellationToken);
    var activeSources = sources.Where(s => !s.IsPaused).Select(s => s.Source).ToArray();

    var results = new List<AdminActionResult<QueueRequestOverview>>();
    foreach (var source in activeSources)
    {
        var crawlRequest = new AdminManualCrawlRequest
        {
            Source = source,
            Keyword = request.Keyword,
            TriggeredBy = request.TriggeredBy,
            Location = request.Location,
            SalaryRange = request.SalaryRange,
            Tags = request.Tags,
            ExperienceLevel = request.ExperienceLevel,
            JobType = request.JobType,
            ProxyUrls = request.ProxyUrls
        };
        var result = await client.TriggerManualCrawlAsync(crawlRequest, cancellationToken);
        results.Add(result);
    }

    var successCount = results.Count(r => r.Success);
    var message = $"Triggered crawl for {successCount}/{activeSources.Length} sources.";

    await auditLogService.AppendAsync(
        action: "crawl-all-sources",
        target: "all",
        actor: string.IsNullOrWhiteSpace(request.TriggeredBy) ? "admin-dashboard" : request.TriggeredBy,
        success: successCount > 0,
        details: message,
        cancellationToken);

    return Results.Ok(new
    {
        message,
        results,
        triggeredSources = activeSources,
        successCount,
        totalCount = activeSources.Length
    });
}).RequireAuthorization("AdminOperator");

app.MapPost("/api/admin/crawl-requests/{id:guid}/retry", async (
    Guid id,
    OrchestrationOperationsClient client,
    AdminAuditLogService auditLogService,
    CancellationToken cancellationToken) =>
{
    var success = await client.RetryFailedRequestAsync(id, cancellationToken);
    await auditLogService.AppendAsync(
        action: "retry-crawl-request",
        target: id.ToString(),
        actor: "admin-dashboard",
        success: success,
        details: success ? "Retry queued from admin dashboard." : "Retry request not found.",
        cancellationToken);
    return success ? Results.Ok(new { message = "Retry queued." }) : Results.NotFound();
}).RequireAuthorization("AdminOperator");

app.MapPost("/api/admin/sources/{source}/pause", async (
    string source,
    UpdateSourceStateRequest request,
    OrchestrationOperationsClient client,
    AdminAuditLogService auditLogService,
    CancellationToken cancellationToken) =>
{
    var result = await client.PauseSourceAsync(source, request.Reason, cancellationToken);
    await auditLogService.AppendAsync(
        action: "pause-source",
        target: source,
        actor: "admin-dashboard",
        success: result is not null,
        details: string.IsNullOrWhiteSpace(request.Reason)
            ? "Source paused without a note."
            : $"Source paused with note: {request.Reason}",
        cancellationToken);
    return Results.Ok(result);
}).RequireAuthorization("AdminOperator");

app.MapPost("/api/admin/sources/{source}/resume", async (
    string source,
    OrchestrationOperationsClient client,
    AdminAuditLogService auditLogService,
    CancellationToken cancellationToken) =>
{
    var result = await client.ResumeSourceAsync(source, cancellationToken);
    await auditLogService.AppendAsync(
        action: "resume-source",
        target: source,
        actor: "admin-dashboard",
        success: result is not null,
        details: "Source resumed from admin dashboard.",
        cancellationToken);
    return Results.Ok(result);
}).RequireAuthorization("AdminOperator");

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

static string? ExtractBearerToken(HttpContext httpContext)
{
    var header = httpContext.Request.Headers.Authorization.ToString();
    if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        return null;
    }

    return header["Bearer ".Length..].Trim();
}
