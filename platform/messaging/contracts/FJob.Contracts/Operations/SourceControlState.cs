namespace FJob.Contracts.Operations;

public sealed class SourceControlState
{
    public string Source { get; init; } = string.Empty;
    public bool IsPaused { get; init; }
    public string? Reason { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}
