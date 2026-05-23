using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using FJob.ApiGateway.Services;
using FJob.Contracts.Crawl;
using FJob.Contracts.Search;
using FJob.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddEnvironmentVariables();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddProblemDetails();
builder.Services.AddFJobObservability();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "App_Data", "DataProtectionKeys")))
    .SetApplicationName("FJob");
builder.Services.Configure<ApiGatewayOptions>(builder.Configuration.GetSection(ApiGatewayOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<AiAdvisorOptions>(builder.Configuration.GetSection(AiAdvisorOptions.SectionName));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddSingleton<CvAdviceService>();
builder.Services.AddHttpClient<OllamaClient>((serviceProvider, httpClient) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<AiAdvisorOptions>>().Value;
    httpClient.BaseAddress = new Uri(options.BaseUrl);
});
builder.Services.AddHttpClient<JobSearchClient>();
builder.Services.AddHttpClient<JobCatalogClient>();
builder.Services.AddHttpClient<CrawlOrchestrationClient>();
builder.Services.AddHttpClient<IdentityAccessClient>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.RequestServices.GetRequiredService<ServiceMetrics>().RecordRateLimitedRequest();
        return ValueTask.CompletedTask;
    };
    options.AddFixedWindowLimiter("search", limiterOptions =>
    {
        limiterOptions.PermitLimit = 30;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
});

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
    });
builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policyBuilder =>
        policyBuilder
            .WithOrigins("http://localhost:5105")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseFJobObservability();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new
{
    service = "api-gateway",
    status = "healthy",
    time = DateTimeOffset.UtcNow
}));

app.MapGet("/metrics", (ServiceMetrics serviceMetrics) =>
{
    return Results.Ok(new
    {
        service = "api-gateway",
        metrics = serviceMetrics.Snapshot()
    });
});

static bool IsMostlyGibberish(string? s)
{
    if (string.IsNullOrEmpty(s)) return false;
    int total = s.Length;
    if (total == 0) return false;
    int printable = 0;
    foreach (var c in s)
    {
        if (!char.IsControl(c) && !char.IsSurrogate(c)) printable++;
    }

    double ratio = (double)printable / total;
    return ratio < 0.65; // if less than 65% printable, treat as gibberish
}

app.MapGet("/ready", async (
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var gatewaySection = configuration.GetSection(ApiGatewayOptions.SectionName);
    var jobSearchBaseUrl = gatewaySection["JobSearchBaseUrl"] ?? "http://localhost:5103";
    var jobCatalogBaseUrl = gatewaySection["JobCatalogBaseUrl"] ?? "http://localhost:5101";
    var crawlOrchestrationBaseUrl = gatewaySection["CrawlOrchestrationBaseUrl"] ?? "http://localhost:5102";
    var httpClient = httpClientFactory.CreateClient();

    try
    {
        var jobSearchResponse = await httpClient.GetAsync(new Uri(new Uri(jobSearchBaseUrl), "/health"), cancellationToken);
        var jobCatalogResponse = await httpClient.GetAsync(new Uri(new Uri(jobCatalogBaseUrl), "/health"), cancellationToken);
        var orchestrationResponse = await httpClient.GetAsync(new Uri(new Uri(crawlOrchestrationBaseUrl), "/health"), cancellationToken);

        jobSearchResponse.EnsureSuccessStatusCode();
        jobCatalogResponse.EnsureSuccessStatusCode();
        orchestrationResponse.EnsureSuccessStatusCode();

        return Results.Ok(new
        {
            service = "api-gateway",
            ready = true,
            dependencies = new
            {
                jobSearch = "reachable",
                jobCatalog = "reachable",
                crawlOrchestration = "reachable"
            },
            time = DateTimeOffset.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            service = "api-gateway",
            ready = false,
            error = ex.Message,
            time = DateTimeOffset.UtcNow
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/api/jobs/search", async (
    SearchQueryRequest request,
    JobSearchClient jobSearchClient,
    JobCatalogClient jobCatalogClient,
    CrawlOrchestrationClient orchestrationClient,
    IOptions<ApiGatewayOptions> options,
    CancellationToken cancellationToken) =>
{
    var sources = request.Sources
        .Where(source => !string.IsNullOrWhiteSpace(source))
        .Select(source => source.Trim())
        .ToArray();

    if (sources.Length == 0)
    {
        sources = options.Value.KnownSources ?? Array.Empty<string>();
    }

    if (sources.Length == 0)
    {
        return Results.BadRequest(new { message = "No crawl sources are configured for this search." });
    }

    // Only trigger crawl/clear when the request explicitly asks for it. This prevents
    // accidental re-crawls on page reloads or when users navigate back to page 1.
    if (request.TriggerCrawl && request.Page <= 1)
    {
        await jobCatalogClient.ClearJobsAsync(cancellationToken);

        var queuedRequests = new List<QueuedCrawlRequestState>();
        foreach (var source in sources)
        {
            var crawlRequest = new CrawlRequestMessage
            {
                RequestId = Guid.NewGuid(),
                Source = source,
                Keyword = request.Keyword,
                Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location,
                SalaryRange = BuildSalaryRange(request.SalaryMinMillions, request.SalaryMaxMillions),
                Tags = request.Tags?.Length > 0 ? request.Tags : null,
                TriggeredBy = "search",
                RequestedAtUtc = DateTimeOffset.UtcNow,
                TraceId = Guid.NewGuid().ToString("N"),
                MaxPages = request.MaxPages.HasValue ? Math.Clamp(request.MaxPages.Value, 5, 50) : 5
            };

            var queuedState = await orchestrationClient.EnqueueAsync(
                crawlRequest,
                preferDirectHttp: true,
                cancellationToken);
            queuedRequests.Add(queuedState);
        }

        foreach (var queuedState in queuedRequests)
        {
            await WaitForRequestCompletionAsync(queuedState.RequestId, orchestrationClient, cancellationToken);
        }

        await jobSearchClient.RebuildIndexAsync(cancellationToken);
    }
    else if (request.FilterOnly)
    {
        // No crawling or rebuild; search will consult existing catalog/index only.
    }

    var response = await jobSearchClient.SearchAsync(request, cancellationToken);
    return Results.Json(response, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });
}).RequireRateLimiting("search").AllowAnonymous();

app.MapPost("/api/uploads/parse-cv", async (
    HttpRequest req,
    IHttpClientFactory httpClientFactory,
    IOptions<ApiGatewayOptions> options,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    // Strict MIME type and file extension whitelist
    string[] allowedExtensions = [".txt", ".pdf", ".docx"];
    string[] allowedMimeTypes = [
        "text/plain", 
        "application/pdf", 
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        ];
    const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    if (!req.HasFormContentType)
    {
        return Results.BadRequest(new { message = "multipart/form-data required." });
    }

    var form = await req.ReadFormAsync(cancellationToken);
    var file = form.Files.FirstOrDefault();
    if (file is null)
    {
        return Results.BadRequest(new { message = "No file uploaded." });
    }

    // Validate file size
    if (file.Length > MaxFileSizeBytes)
    {
        logger.LogWarning("CV upload rejected: file size {FileSize} exceeds limit {MaxSize}.", file.Length, MaxFileSizeBytes);
        return Results.BadRequest(new { message = $"File size must not exceed {MaxFileSizeBytes / (1024 * 1024)} MB." });
    }

    if (file.Length == 0)
    {
        logger.LogWarning("CV upload rejected: file is empty.");
        return Results.BadRequest(new { message = "File is empty." });
    }

    var fileName = file.FileName ?? string.Empty;
    var fileNameLower = fileName.ToLowerInvariant();
    var contentType = file.ContentType ?? string.Empty;

    // Validate file extension (whitelist)
    var hasValidExtension = allowedExtensions.Any(ext => fileNameLower.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    if (!hasValidExtension)
    {
        logger.LogWarning("CV upload rejected: invalid file extension '{FileName}'.", fileName);
        return Results.BadRequest(new { message = "Only .txt, .pdf, and .docx files are allowed." });
    }

    // Validate MIME type (basic check; can be spoofed but combined with extension check is safer)
    if (!string.IsNullOrEmpty(contentType) && !allowedMimeTypes.Any(m => contentType.Contains(m, StringComparison.OrdinalIgnoreCase)))
    {
        logger.LogWarning("CV upload rejected: suspicious MIME type '{ContentType}' for file '{FileName}'.", contentType, fileName);
        return Results.BadRequest(new { message = "Invalid file type." });
    }

    // Additional check: prevent double extension attacks (e.g., file.pdf.exe)
    var extensionCount = allowedExtensions.Count(ext => fileNameLower.Contains(ext, StringComparison.OrdinalIgnoreCase));
    if (extensionCount > 1)
    {
        logger.LogWarning("CV upload rejected: multiple extensions detected in '{FileName}'.", fileName);
        return Results.BadRequest(new { message = "Invalid filename format." });
    }

    await using var ms = new MemoryStream();
    await file.CopyToAsync(ms, cancellationToken);
    ms.Position = 0;
    string text = string.Empty;

    try
    {
        if (fileNameLower.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
        {
            ms.Position = 0;
            using var doc = WordprocessingDocument.Open(ms, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body is not null)
            {
                var sb = new StringBuilder();
                var paras = body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>();
                foreach (var p in paras)
                {
                    var runs = p.Descendants<DocumentFormat.OpenXml.Wordprocessing.Run>();
                    foreach (var r in runs)
                    {
                        var texts = r.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>();
                        foreach (var t in texts)
                        {
                            sb.Append(t.Text);
                        }
                    }
                    sb.AppendLine();
                }

                text = sb.ToString();
            }
        }
        else if (fileNameLower.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            ms.Position = 0;
            using var pdf = PdfDocument.Open(ms);
            var sb = new StringBuilder();
            foreach (var page in pdf.GetPages())
            {
                var pageText = page.Text ?? string.Empty;

                if (IsMostlyGibberish(pageText))
                {
                    try
                    {
                        var words = page.GetWords();
                        if (words != null)
                        {
                            var wtext = string.Join(' ', words.Select(w => w.Text));
                            if (!string.IsNullOrWhiteSpace(wtext))
                            {
                                pageText = wtext;
                            }
                        }
                    }
                    catch
                    {
                        // ignore and fall back to raw page.Text
                    }
                }

                sb.AppendLine(pageText);
            }

            text = sb.ToString();
        }
        else if (fileNameLower.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            ms.Position = 0;
            using var reader = new StreamReader(ms);
            text = await reader.ReadToEndAsync(cancellationToken);
        }

        // If PDF looks like gibberish and OCR service is configured, send to OCR
        if (fileNameLower.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && IsMostlyGibberish(text))
        {
            try
            {
                var ocrUrl = options.Value.OcrServiceBaseUrl?.TrimEnd('/') ?? "http://ocr-service:5000";
                var client = httpClientFactory.CreateClient();
                ms.Position = 0;
                using var content = new MultipartFormDataContent();
                var streamContent = new StreamContent(new MemoryStream(ms.ToArray()));
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType ?? "application/pdf");
                content.Add(streamContent, "file", fileName);

                using var ocrResp = await client.PostAsync(new Uri(new Uri(ocrUrl), "/api/ocr/parse"), content, cancellationToken);
                if (ocrResp.IsSuccessStatusCode)
                {
                    var obj = await ocrResp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                    if (obj.TryGetProperty("text", out var txt))
                    {
                        text = txt.GetString() ?? text;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "OCR service call failed; falling back to extracted text.");
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error parsing CV file '{FileName}'.", fileName);
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }

    // Validate extracted text is not empty or suspicious
    if (string.IsNullOrWhiteSpace(text))
    {
        logger.LogWarning("CV upload rejected: no readable text extracted from '{FileName}'.", fileName);
        return Results.BadRequest(new { message = "File contains no readable text." });
    }

    logger.LogInformation("CV file '{FileName}' successfully parsed ({TextLength} characters).", fileName, text.Length);
    return Results.Ok(new { text });
}).AllowAnonymous().WithName("ParseCV");


static string? BuildSalaryRange(decimal? min, decimal? max)
{
    if (min.HasValue && max.HasValue)
    {
        return $"{min.Value}-{max.Value}";
    }

    if (min.HasValue)
    {
        return $"{min.Value}+";
    }

    if (max.HasValue)
    {
        return $"0-{max.Value}";
    }

    return null;
}

static async Task<QueuedCrawlRequestState> WaitForRequestCompletionAsync(
    Guid requestId,
    CrawlOrchestrationClient orchestrationClient,
    CancellationToken cancellationToken)
{
    while (true)
    {
        var queuedState = await orchestrationClient.GetRequestAsync(requestId, cancellationToken);
        if (queuedState is null)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            continue;
        }

        if (queuedState.Status == QueueRequestStatus.Completed || queuedState.Status == QueueRequestStatus.Failed)
        {
            return queuedState;
        }

        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
    }
}

app.MapMethods("/api/jobs/search", ["OPTIONS"], () => Results.Ok()).AllowAnonymous();

app.MapPost("/api/jobs/cv-advice", async (
    CvAdviceRequest request,
    CvAdviceService cvAdviceService,
    OllamaClient ollamaClient,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.CvText))
    {
        return Results.BadRequest(new { message = "CV text is required." });
    }

    CvAdviceResponse advice;
    try
    {
        advice = await ollamaClient.AnalyzeAsync(request, cancellationToken)
            ?? cvAdviceService.Analyze(request);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Unexpected error while processing CV advice. Falling back to built-in analyzer.");
        advice = cvAdviceService.Analyze(request);
    }

    return Results.Ok(advice);
}).AllowAnonymous();

app.MapPost("/api/auth/login", async (
    HttpContext httpContext,
    IdentityAccessClient identityAccessClient,
    CancellationToken cancellationToken) =>
{
    var payload = await httpContext.Request.ReadFromJsonAsync<object>(cancellationToken: cancellationToken);
    if (payload is null)
    {
        return Results.BadRequest(new { message = "Yêu cầu đăng nhập không hợp lệ." });
    }

    using var response = await identityAccessClient.LoginAsync(payload, cancellationToken);
    var content = await response.Content.ReadAsStringAsync(cancellationToken);
    return Results.Content(
        content,
        response.Content.Headers.ContentType?.ToString() ?? "application/json",
        Encoding.UTF8,
        (int)response.StatusCode);
}).AllowAnonymous();

app.MapPost("/api/auth/register", async (
    HttpContext httpContext,
    IdentityAccessClient identityAccessClient,
    CancellationToken cancellationToken) =>
{
    var payload = await httpContext.Request.ReadFromJsonAsync<object>(cancellationToken: cancellationToken);
    if (payload is null)
    {
        return Results.BadRequest(new { message = "Yêu cầu đăng ký không hợp lệ." });
    }

    using var response = await identityAccessClient.RegisterAsync(payload, cancellationToken);
    var content = await response.Content.ReadAsStringAsync(cancellationToken);
    return Results.Content(
        content,
        response.Content.Headers.ContentType?.ToString() ?? "application/json",
        Encoding.UTF8,
        (int)response.StatusCode);
}).AllowAnonymous();

app.MapPost("/api/auth/logout", async (
    HttpContext httpContext,
    IdentityAccessClient identityAccessClient,
    CancellationToken cancellationToken) =>
{
    var bearerToken = httpContext.Request.Headers.Authorization
        .FirstOrDefault()?
        .Replace("Bearer ", string.Empty, StringComparison.OrdinalIgnoreCase);

    using var response = await identityAccessClient.LogoutAsync(bearerToken, cancellationToken);
    var content = await response.Content.ReadAsStringAsync(cancellationToken);
    return Results.Content(
        content,
        response.Content.Headers.ContentType?.ToString() ?? "application/json",
        Encoding.UTF8,
        (int)response.StatusCode);
}).RequireAuthorization();

app.MapGet("/api/auth/me", (ClaimsPrincipal principal) =>
{
    return Results.Ok(new
    {
        username = principal.Identity?.Name ?? string.Empty,
        role = principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty
    });
}).RequireAuthorization();

app.Run();
