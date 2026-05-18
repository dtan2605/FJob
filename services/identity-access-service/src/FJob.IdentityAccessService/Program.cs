using System.Security.Claims;
using System.Text;
using FJob.IdentityAccessService;
using FJob.IdentityAccessService.Contracts;
using FJob.IdentityAccessService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
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
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "App_Data", "DataProtectionKeys")))
    .SetApplicationName("FJob");
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<IdentityOptions>(builder.Configuration.GetSection(IdentityOptions.SectionName));
builder.Services.AddSingleton<MySqlConnectionFactory>();
builder.Services.AddSingleton<IdentityRepository>();
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddSingleton<JwtTokenIssuer>();

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
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.RequestServices.GetRequiredService<ServiceMetrics>().RecordRateLimitedRequest();
        return ValueTask.CompletedTask;
    };
    options.AddFixedWindowLimiter("login", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
});

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
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new
{
    service = "identity-access-service",
    status = "healthy",
    time = DateTimeOffset.UtcNow
}));

app.MapGet("/metrics", (ServiceMetrics serviceMetrics) =>
{
    return Results.Ok(new
    {
        service = "identity-access-service",
        metrics = serviceMetrics.Snapshot()
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
            service = "identity-access-service",
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
            service = "identity-access-service",
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

app.MapPost("/api/auth/login", async (
    LoginRequest request,
    IdentityRepository repository,
    JwtTokenIssuer tokenIssuer,
    CancellationToken cancellationToken) =>
{
    var user = await repository.GetByUsernameAsync(request.Username, cancellationToken);
    if (user is null || !user.IsActive || !PasswordHasher.Verify(request.Password, user.PasswordHash))
    {
        await repository.AppendAuditAsync(
            "login",
            request.Username,
            success: false,
            details: "Invalid username or password.",
            cancellationToken);

        return Results.Unauthorized();
    }

    var token = tokenIssuer.CreateToken(user);
    await repository.AppendAuditAsync(
        "login",
        user.Username,
        success: true,
        details: $"Role {user.Role} login successful.",
        cancellationToken);

    return Results.Ok(new LoginResponse
    {
        AccessToken = token.Token,
        ExpiresAtUtc = token.ExpiresAtUtc,
        Username = user.Username,
        Role = user.Role
    });
}).RequireRateLimiting("login");

app.MapPost("/api/auth/register", async (
    RegisterRequest request,
    IdentityRepository repository,
    CancellationToken cancellationToken) =>
{
    var username = request.Username.Trim();
    if (string.IsNullOrWhiteSpace(username))
    {
        return Results.BadRequest(new { message = "Tên đăng nhập không được để trống." });
    }

    if (request.Password.Length < 8)
    {
        return Results.BadRequest(new { message = "Mật khẩu phải có ít nhất 8 ký tự." });
    }

    if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
    {
        return Results.BadRequest(new { message = "Mật khẩu xác nhận không khớp." });
    }

    if (await repository.UsernameExistsAsync(username, cancellationToken))
    {
        await repository.AppendAuditAsync(
            "register",
            username,
            success: false,
            details: "Username already exists.",
            cancellationToken);

        return Results.Conflict(new { message = "Tên đăng nhập đã tồn tại." });
    }

    var user = await repository.CreateUserAsync(username, request.Password, "candidate", cancellationToken);
    await repository.AppendAuditAsync(
        "register",
        username,
        success: true,
        details: "Candidate account created.",
        cancellationToken);

    return Results.Created($"/api/auth/users/{user.Id}", new RegisterResponse
    {
        Username = user.Username,
        Role = user.Role,
        CreatedAtUtc = user.CreatedAtUtc
    });
}).RequireRateLimiting("login");

app.MapPost("/api/auth/logout", async (
    ClaimsPrincipal principal,
    IdentityRepository repository,
    CancellationToken cancellationToken) =>
{
    var username = principal.Identity?.Name ?? "unknown";
    await repository.AppendAuditAsync(
        "logout",
        username,
        success: true,
        details: "Logout recorded.",
        cancellationToken);

    return Results.Ok(new { message = "Logout recorded." });
}).RequireAuthorization();

app.MapGet("/api/auth/me", (ClaimsPrincipal principal) =>
{
    return Results.Ok(new CurrentUserResponse
    {
        Username = principal.Identity?.Name ?? string.Empty,
        Role = principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty
    });
}).RequireAuthorization();

app.Run();
