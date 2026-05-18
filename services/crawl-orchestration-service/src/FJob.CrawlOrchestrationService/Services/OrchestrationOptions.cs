namespace FJob.CrawlOrchestrationService.Services;

public sealed class OrchestrationOptions
{
    public const string SectionName = "Orchestration";

    public string JobCatalogBaseUrl { get; init; } = "https://localhost:7101";
    public string PythonExecutable { get; init; } = "python";
    public string ExecutionScriptPath { get; init; } =
        Path.Combine("..", "..", "..", "crawl-execution-service", "src", "main.py");
    public int PollingIntervalSeconds { get; init; } = 5;
    public int MaxAttempts { get; init; } = 3;
    public bool UseRabbitMq { get; init; } = false;
    public string RabbitMqConnectionString { get; init; } = "amqp://guest:guest@rabbitmq:5672";
    public string RabbitMqQueueName { get; init; } = "fjob.crawl.requests";
    public string[] KnownSources { get; init; } = ["TopCV", "Vieclam24h", "Careerviet", "Indeed"];
}
