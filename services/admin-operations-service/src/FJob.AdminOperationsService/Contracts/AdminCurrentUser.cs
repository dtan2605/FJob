namespace FJob.AdminOperationsService.Contracts;

public sealed class AdminCurrentUser
{
    public string Username { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}
