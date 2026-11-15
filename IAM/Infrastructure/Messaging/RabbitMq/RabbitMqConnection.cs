using foll_iam.IAM.Infrastructure.Messaging.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace foll_iam.IAM.Infrastructure.Messaging.RabbitMq;

// One connection/channel per service instance; the gate serializes channel use.
public sealed class RabbitMqConnection(
    IOptions<RabbitMqOptions> options, IEnumerable<string> routingKeys,
    ILogger<RabbitMqConnection> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;
    private bool _disposed;

    public async Task PublishAsync(string eventType, Guid eventId, Guid operationId,
        DateTime occurredAtUtc, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.PublishTimeoutSeconds));
        await _gate.WaitAsync(timeout.Token);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await EnsureConnectedAsync(timeout.Token);
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                Type = eventType,
                MessageId = eventId.ToString(),
                CorrelationId = operationId.ToString(),
                Timestamp = new AmqpTimestamp(new DateTimeOffset(occurredAtUtc).ToUnixTimeSeconds())
            };
            // With confirmation tracking, await completes after broker confirmation and
            // throws for a nack or mandatory return (unroutable message).
            await _channel!.BasicPublishAsync(options.Value.ExchangeName, eventType,
                mandatory: true, basicProperties: properties, body: body,
                cancellationToken: timeout.Token);
        }
        catch
        {
            await ResetAsync();
            throw;
        }
        finally { _gate.Release(); }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_connection?.IsOpen == true && _channel?.IsOpen == true) return;
        await ResetAsync();
        var settings = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = settings.HostName, Port = settings.Port,
            UserName = settings.UserName, Password = settings.Password,
            VirtualHost = settings.VirtualHost,
            // The outbox owns retries and reopens failed connections.
            AutomaticRecoveryEnabled = false,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(settings.PublishTimeoutSeconds),
            ContinuationTimeout = TimeSpan.FromSeconds(settings.PublishTimeoutSeconds)
        };
        _connection = await factory.CreateConnectionAsync("foll-IAM-outbox", cancellationToken);
        _channel = await _connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), cancellationToken);
        await _channel.ExchangeDeclareAsync(settings.ExchangeName, ExchangeType.Topic,
            durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await _channel.QueueDeclareAsync(settings.QueueName, durable: true,
            exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        foreach (var key in routingKeys)
            await _channel.QueueBindAsync(settings.QueueName, settings.ExchangeName, key,
                cancellationToken: cancellationToken);
    }

    private async Task ResetAsync()
    {
        if (_channel is not null)
        {
            try { await _channel.DisposeAsync(); }
            catch (Exception ex) { logger.LogDebug(ex, "Error cerrando el canal RabbitMQ."); }
            _channel = null;
        }
        if (_connection is not null)
        {
            try { await _connection.DisposeAsync(); }
            catch (Exception ex) { logger.LogDebug(ex, "Error cerrando la conexion RabbitMQ."); }
            _connection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try { _disposed = true; await ResetAsync(); }
        finally { _gate.Release(); }
    }
}