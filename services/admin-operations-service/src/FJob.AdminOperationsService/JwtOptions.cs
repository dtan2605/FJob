namespace FJob.AdminOperationsService;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "FJob.Identity";
    public string Audience { get; init; } = "FJob.Admin";
    public string SigningKey { get; init; } = "fjob-local-development-signing-key-please-change-2026";
}
