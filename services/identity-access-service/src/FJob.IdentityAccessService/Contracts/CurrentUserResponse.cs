namespace FJob.IdentityAccessService.Contracts;

public sealed class CurrentUserResponse
{
    public string Username { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}
