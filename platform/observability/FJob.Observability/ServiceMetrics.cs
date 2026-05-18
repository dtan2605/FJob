using System.Threading;

namespace FJob.Observability;

public sealed class ServiceMetrics
{
    private long _totalRequests;
    private long _failedRequests;
    private long _rateLimitedRequests;
    private long _totalResponseTicks;

    public void RecordRequest(int statusCode, TimeSpan duration)
    {
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Add(ref _totalResponseTicks, duration.Ticks);

        if (statusCode >= 400)
        {
            Interlocked.Increment(ref _failedRequests);
        }
    }

    public void RecordRateLimitedRequest()
    {
        Interlocked.Increment(ref _rateLimitedRequests);
    }

    public ServiceMetricsSnapshot Snapshot()
    {
        var totalRequests = Interlocked.Read(ref _totalRequests);
        var totalTicks = Interlocked.Read(ref _totalResponseTicks);

        return new ServiceMetricsSnapshot
        {
            TotalRequests = totalRequests,
            FailedRequests = Interlocked.Read(ref _failedRequests),
            RateLimitedRequests = Interlocked.Read(ref _rateLimitedRequests),
            AverageResponseMilliseconds = totalRequests == 0
                ? 0
                : TimeSpan.FromTicks(totalTicks / totalRequests).TotalMilliseconds,
            CapturedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
