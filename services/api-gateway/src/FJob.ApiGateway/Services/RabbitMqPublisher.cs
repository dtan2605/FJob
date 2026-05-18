using System.Text;
using System.Text.Json;
using FJob.Contracts.Crawl;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FJob.ApiGateway.Services;

public sealed class RabbitMqPublisher
{
    private readonly ApiGatewayOptions _options;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public RabbitMqPublisher(
        IOptions<ApiGatewayOptions> options,
        ILogger<RabbitMqPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<QueuedCrawlRequestState> PublishAsync(
        CrawlRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestId == Guid.Empty)
        {
            throw new InvalidOperationException("CrawlRequestMessage must contain a RequestId before publishing.");
        }

        var factory = new ConnectionFactory
        {
            Uri = new Uri(_options.RabbitMqConnectionString)
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(false, false, null, 0), cancellationToken);

        await channel.QueueDeclareAsync(
            _options.RabbitMqQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var payload = JsonSerializer.Serialize(request, _serializerOptions);
        var body = Encoding.UTF8.GetBytes(payload);

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _options.RabbitMqQueueName,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Published crawl request {RequestId} to RabbitMQ queue {QueueName}.", request.RequestId, _options.RabbitMqQueueName);

        return new QueuedCrawlRequestState
        {
            RequestId = request.RequestId,
            Source = request.Source,
            TriggeredBy = request.TriggeredBy,
            TraceId = request.TraceId,
            RequestedAtUtc = request.RequestedAtUtc,
            Location = request.Location,
            SalaryRange = request.SalaryRange,
            Tags = request.Tags,
            ExperienceLevel = request.ExperienceLevel,
            JobType = request.JobType,
            Status = QueueRequestStatus.Pending,
            AttemptCount = 0,
            NextAttemptAtUtc = DateTimeOffset.UtcNow
        };
    }
}
