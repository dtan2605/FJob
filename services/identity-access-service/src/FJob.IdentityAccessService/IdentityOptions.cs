namespace FJob.IdentityAccessService;

public sealed class IdentityOptions
{
    public const string SectionName = "Identity";

    public SeedUser[] SeedUsers { get; init; } = [];
}

public sealed class SeedUser
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Role { get; init; } = "readonly";
}
