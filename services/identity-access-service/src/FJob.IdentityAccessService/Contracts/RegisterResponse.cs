namespace FJob.IdentityAccessService.Contracts;

public sealed class RegisterResponse
{
    public string Username { get; init; } = string.Empty;
    public string Role { get; init; } = "candidate";
    public DateTimeOffset CreatedAtUtc { get; init; }
}
