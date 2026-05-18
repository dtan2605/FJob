using System.IO.Compression;
using FJob.FrontendWeb;
using FJob.Observability;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Options;

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
builder.Services.AddHttpClient();
builder.Services
    .AddOptions<FrontendOptions>()
    .Bind(builder.Configuration.GetSection(FrontendOptions.SectionName))
    .Validate(
        options => Uri.TryCreate(options.ApiGatewayBaseUrl, UriKind.Absolute, out _),
        "Frontend:ApiGatewayBaseUrl must be an absolute URL.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.AppName), "Frontend:AppName is required.")
    .ValidateOnStart();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

var app = builder.Build();

app.UseExceptionHandler();
app.UseResponseCompression();
app.UseFJobObservability();
app.Use(async (context, next) =>
{
    var frontendOptions = context.RequestServices.GetRequiredService<IOptions<FrontendOptions>>().Value;
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    context.Response.Headers.Append("Content-Security-Policy", BuildContentSecurityPolicy(frontendOptions.ApiGatewayBaseUrl));

    await next();
});

app.MapGet("/health", () => Results.Ok(new
{
    service = "frontend-web",
    status = "healthy",
    time = DateTimeOffset.UtcNow
}));

app.MapGet("/ready", async (
    IHttpClientFactory httpClientFactory,
    IOptions<FrontendOptions> frontendOptionsAccessor,
    CancellationToken cancellationToken) =>
{
    var frontendOptions = frontendOptionsAccessor.Value;
    var httpClient = httpClientFactory.CreateClient();

    try
    {
        var gatewayHealth = await ResilienceExecutor.ExecuteAsync(
            "gateway-health-check",
            app.Logger,
            async ct =>
            {
                // Use container network URL for server-side health check
                using var response = await httpClient.GetAsync(
                    "http://api-gateway:8080/health",
                    ct);

                response.EnsureSuccessStatusCode();
                return true;
            },
            cancellationToken,
            maxAttempts: 2,
            timeoutSeconds: 3);

        return Results.Ok(new
        {
            service = "frontend-web",
            ready = gatewayHealth,
            dependencies = new
            {
                apiGateway = "reachable"
            },
            time = DateTimeOffset.UtcNow
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Frontend readiness check failed while probing API Gateway.");
        return Results.Json(new
        {
            service = "frontend-web",
            ready = false,
            dependencies = new
            {
                apiGateway = "unreachable"
            },
            error = ex.Message,
            time = DateTimeOffset.UtcNow
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapGet("/metrics", (
    ServiceMetrics serviceMetrics,
    IOptions<FrontendOptions> frontendOptionsAccessor) =>
{
    var frontendOptions = frontendOptionsAccessor.Value;
    return Results.Ok(new
    {
        service = "frontend-web",
        app = frontendOptions.AppName,
        renderingMode = "angular-standalone-spa",
        apiGatewayBaseUrl = frontendOptions.ApiGatewayBaseUrl,
        metrics = serviceMetrics.Snapshot()
    });
});

app.MapGet("/api/config", (
    HttpContext httpContext,
    IOptions<FrontendOptions> frontendOptionsAccessor) =>
{
    httpContext.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";

    var frontendOptions = frontendOptionsAccessor.Value;
    return Results.Ok(new
    {
        apiGatewayBaseUrl = frontendOptions.ApiGatewayBaseUrl,
        appName = frontendOptions.AppName,
        renderingMode = "angular-standalone-spa"
    });
});

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        var path = context.File.Name;
        if (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            context.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            return;
        }

        if (path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
        {
            context.Context.Response.Headers["Cache-Control"] = "public, max-age=3600";
        }
    }
});
app.MapFallbackToFile("index.html");

app.Run();

static string BuildContentSecurityPolicy(string apiGatewayBaseUrl)
{
    var connectSources = new List<string> { "'self'", "http://localhost:5100" };
    if (Uri.TryCreate(apiGatewayBaseUrl, UriKind.Absolute, out var gatewayUri))
    {
        connectSources.Add($"{gatewayUri.Scheme}://{gatewayUri.Authority}");
    }

    return string.Join(
        "; ",
        [
            "default-src 'self'",
            "base-uri 'self'",
            "object-src 'none'",
            "frame-ancestors 'none'",
            "img-src 'self' data:",
            "style-src 'self' 'unsafe-inline'",
            "font-src 'self'",
            "script-src 'self'",
            $"connect-src {string.Join(" ", connectSources.Distinct(StringComparer.OrdinalIgnoreCase))}",
            "form-action 'self'"
        ]);
}
