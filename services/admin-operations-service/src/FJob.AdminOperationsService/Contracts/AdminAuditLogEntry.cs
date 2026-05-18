namespace FJob.AdminOperationsService.Contracts;

public sealed class AdminAuditLogEntry
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
    public string Actor { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Details { get; init; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; init; }
}
