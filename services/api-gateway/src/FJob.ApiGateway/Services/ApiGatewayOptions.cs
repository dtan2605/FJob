namespace FJob.ApiGateway.Services;

public sealed class ApiGatewayOptions
{
    public const string SectionName = "Gateway";

    public string JobSearchBaseUrl { get; init; } = "http://localhost:5103";
    public string JobCatalogBaseUrl { get; init; } = "http://localhost:5101";
    public string CrawlOrchestrationBaseUrl { get; init; } = "http://localhost:5102";
    public string IdentityAccessBaseUrl { get; init; } = "http://localhost:5104";
    public string OcrServiceBaseUrl { get; init; } = "http://ocr-service:5000";
    public bool UseRabbitMq { get; init; } = false;
    public string RabbitMqConnectionString { get; init; } = "amqp://guest:guest@rabbitmq:5672";
    public string RabbitMqQueueName { get; init; } = "fjob.crawl.requests";
    public string[] KnownSources { get; init; } = new[] { "TopCV", "Vieclam24h", "Careerviet", "Indeed" };
}
