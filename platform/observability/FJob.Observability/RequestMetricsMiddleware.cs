using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace FJob.Observability;

public sealed class RequestMetricsMiddleware(
    RequestDelegate next,
    ServiceMetrics serviceMetrics)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            serviceMetrics.RecordRequest(context.Response.StatusCode, stopwatch.Elapsed);
        }
    }
}
