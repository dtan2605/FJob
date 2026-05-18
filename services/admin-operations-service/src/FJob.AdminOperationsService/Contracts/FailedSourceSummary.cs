namespace FJob.AdminOperationsService.Contracts;

public sealed class FailedSourceSummary
{
    public string Source { get; init; } = string.Empty;
    public int FailureCount { get; init; }
}
