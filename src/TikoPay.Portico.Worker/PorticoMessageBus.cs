using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using TikoPay.Portico.Persistence;

namespace TikoPay.Portico.Worker;

public sealed class PorticoMessageBusOptions
{
    public const string SectionName = "MessageBus";

    public bool Enabled { get; set; } = true;
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string ExchangeName { get; set; } = "tikopay.integration";
    public string CitadelQueueName { get; set; } = "portico.citadel.events";
    public string CitadelRoutingKeyPattern { get; set; } = "citadel.payment.*";
}

public interface IPorticoMessageBus
{
    Task<int> PullCitadelMessagesIntoInboxAsync(CancellationToken cancellationToken);
    Task<int> PublishPendingOutboxAsync(CancellationToken cancellationToken);
}

public sealed class PorticoRabbitMqMessageBus(
    IServiceScopeFactory scopeFactory,
    IOptions<PorticoMessageBusOptions> options,
    ILogger<PorticoRabbitMqMessageBus> logger) : IPorticoMessageBus, IAsyncDisposable
{
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task<int> PullCitadelMessagesIntoInboxAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return 0;
        }

        var channel = await EnsureChannelAsync(cancellationToken);
        var receivedCount = 0;

        while (!cancellationToken.IsCancellationRequested && receivedCount < 25)
        {
            var result = await channel.BasicGetAsync(options.Value.CitadelQueueName, autoAck: false, cancellationToken: cancellationToken);
            if (result is null)
            {
                break;
            }

            var envelope = new ReceivedIntegrationMessage(
                result.BasicProperties.MessageId ?? Guid.NewGuid().ToString("N"),
                string.IsNullOrWhiteSpace(result.BasicProperties.Type) ? result.RoutingKey : result.BasicProperties.Type,
                result.RoutingKey,
                result.BasicProperties.CorrelationId,
                Encoding.UTF8.GetString(result.Body.ToArray()),
                ReadOccurredAt(result.BasicProperties));

            var stored = false;

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var inboxService = scope.ServiceProvider.GetRequiredService<IIntegrationInboxService>();
                stored = await inboxService.TryStoreAsync(envelope, cancellationToken);
                await channel.BasicAckAsync(result.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
                receivedCount++;

                if (!stored)
                {
                    logger.LogDebug("Skipped duplicate Citadel message {MessageId}.", envelope.MessageId);
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                await channel.BasicNackAsync(result.DeliveryTag, multiple: false, requeue: true, cancellationToken: cancellationToken);
                ResetConnection();
                logger.LogError(ex, "Failed to store Citadel message {MessageId} into Portico inbox.", envelope.MessageId);
                break;
            }
        }

        return receivedCount;
    }

    public async Task<int> PublishPendingOutboxAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return 0;
        }

        var channel = await EnsureChannelAsync(cancellationToken);

        await using var scope = scopeFactory.CreateAsyncScope();
        var outboxService = scope.ServiceProvider.GetRequiredService<IIntegrationOutboxService>();
        var pendingMessages = await outboxService.GetPendingAsync(25, cancellationToken);
        var publishedCount = 0;

        foreach (var message in pendingMessages)
        {
            try
            {
                var properties = new BasicProperties
                {
                    MessageId = message.MessageId,
                    Type = message.MessageType,
                    CorrelationId = string.IsNullOrWhiteSpace(message.CorrelationId) ? null : message.CorrelationId,
                    ContentType = "application/json",
                    DeliveryMode = DeliveryModes.Persistent,
                    Timestamp = new AmqpTimestamp(message.OccurredAt.ToUnixTimeSeconds())
                };

                await channel.BasicPublishAsync(
                    exchange: options.Value.ExchangeName,
                    routingKey: message.RoutingKey,
                    mandatory: false,
                    basicProperties: properties,
                    body: Encoding.UTF8.GetBytes(message.PayloadJson),
                    cancellationToken: cancellationToken);

                await outboxService.MarkPublishedAsync(message.Id, cancellationToken);
                publishedCount++;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                await outboxService.MarkFailedAsync(message.Id, ex.Message, cancellationToken);
                ResetConnection();
                logger.LogError(ex, "Failed to publish Portico outbox message {MessageId}.", message.MessageId);
                break;
            }
        }

        return publishedCount;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _connectionLock.Dispose();
    }

    private async Task<IChannel> EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null && _channel.IsOpen)
        {
            return _channel;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_channel is not null && _channel.IsOpen)
            {
                return _channel;
            }

            ResetConnection();

            var factory = new ConnectionFactory
            {
                HostName = options.Value.HostName,
                Port = options.Value.Port,
                VirtualHost = options.Value.VirtualHost,
                UserName = options.Value.UserName,
                Password = options.Value.Password
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await _channel.ExchangeDeclareAsync(
                exchange: options.Value.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            await _channel.QueueDeclareAsync(
                queue: options.Value.CitadelQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            await _channel.QueueBindAsync(
                queue: options.Value.CitadelQueueName,
                exchange: options.Value.ExchangeName,
                routingKey: options.Value.CitadelRoutingKeyPattern,
                cancellationToken: cancellationToken);

            return _channel;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private void ResetConnection()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        _channel = null;
        _connection = null;
    }

    private static DateTimeOffset ReadOccurredAt(IReadOnlyBasicProperties properties)
    {
        return properties.Timestamp.UnixTime > 0
            ? DateTimeOffset.FromUnixTimeSeconds(properties.Timestamp.UnixTime)
            : DateTimeOffset.UtcNow;
    }
}
