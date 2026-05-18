using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace FJob.Observability;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddFJobObservability(this IServiceCollection services)
    {
        services.AddSingleton<ServiceMetrics>();
        return services;
    }

    public static IApplicationBuilder UseFJobObservability(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<RequestMetricsMiddleware>();
        return app;
    }
}
