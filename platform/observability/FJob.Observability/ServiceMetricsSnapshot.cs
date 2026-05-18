namespace FJob.Observability;

public sealed class ServiceMetricsSnapshot
{
    public long TotalRequests { get; init; }
    public long FailedRequests { get; init; }
    public long RateLimitedRequests { get; init; }
    public double AverageResponseMilliseconds { get; init; }
    public DateTimeOffset CapturedAtUtc { get; init; }
}
