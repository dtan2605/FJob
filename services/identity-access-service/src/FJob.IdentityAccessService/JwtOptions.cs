namespace FJob.IdentityAccessService;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "FJob.Identity";
    public string Audience { get; init; } = "FJob.Admin";
    public string SigningKey { get; init; } = "development-signing-key-change-me-1234567890";
    public int AccessTokenMinutes { get; init; } = 60;
}
