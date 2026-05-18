using System.Text;
using System.Text.Json;
using FJob.Contracts.Crawl;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FJob.CrawlOrchestrationService.Services;

public sealed class RabbitMqCrawlRequestConsumer : BackgroundService
{
    private readonly CrawlRequestQueue _queue;
    private readonly ILogger<RabbitMqCrawlRequestConsumer> _logger;
    private readonly OrchestrationOptions _options;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public RabbitMqCrawlRequestConsumer(
        CrawlRequestQueue queue,
        IOptions<OrchestrationOptions> options,
        ILogger<RabbitMqCrawlRequestConsumer> logger)
    {
        _queue = queue;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.UseRabbitMq)
        {
            _logger.LogInformation("RabbitMQ crawl request consumer is disabled.");
            return;
        }

        try
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(_options.RabbitMqConnectionString)
            };

            _connection = await factory.CreateConnectionAsync(stoppingToken);
            _channel = await _connection.CreateChannelAsync(new CreateChannelOptions(false, false, null, 0), stoppingToken);

            await _channel.QueueDeclareAsync(
                queue: _options.RabbitMqQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, eventArgs) =>
            {
                var body = eventArgs.Body.ToArray();
                var payload = Encoding.UTF8.GetString(body);
                try
                {
                    var request = JsonSerializer.Deserialize<CrawlRequestMessage>(payload, _serializerOptions);
                    if (request is null)
                    {
                        _logger.LogWarning("Received invalid crawl request payload from RabbitMQ: {Payload}", payload);
                        await _channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                        return;
                    }

                    await _queue.EnqueueAsync(request, stoppingToken);
                    await _channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                    _logger.LogInformation("Enqueued crawl request {RequestId} from RabbitMQ to DB queue.", request.RequestId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process RabbitMQ crawl request. Requeueing message.");
                    await _channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
                }
            };

            await _channel.BasicConsumeAsync(
                queue: _options.RabbitMqQueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation("RabbitMQ crawl request consumer started for queue {QueueName}.", _options.RabbitMqQueueName);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
            finally
            {
                if (_channel != null)
                    await _channel.CloseAsync();
                if (_connection != null)
                    await _connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RabbitMQ crawl request consumer encountered an error. Gracefully shutting down.");
        }
    }
}
