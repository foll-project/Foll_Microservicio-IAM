using System.Text.Json;
using foll_iam.IAM.Application.IntegrationEvents;
using foll_iam.IAM.Infrastructure.Messaging.Configuration;
using foll_iam.IAM.Infrastructure.Messaging.RabbitMq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

internal static class RabbitMqChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var settings = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json").AddEnvironmentVariables().Build()
            .GetSection("RabbitMq").Get<RabbitMqOptions>()!;
        var suffix = Guid.NewGuid().ToString("N");
        settings.ExchangeName = "foll.checks.IAM." + suffix;
        settings.QueueName = "foll.checks.IAM." + suffix;
        var keys = new[] { AccountDeletionRequestedIntegrationEvent.EventType };
        var factory = new ConnectionFactory {
            HostName = settings.HostName, Port = settings.Port, UserName = settings.UserName,
            Password = settings.Password, VirtualHost = settings.VirtualHost,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
        };
        await using var admin = await factory.CreateConnectionAsync();
        await using var channel = await admin.CreateChannelAsync();
        await using var transport = new RabbitMqConnection(Options.Create(settings), keys,
            NullLogger<RabbitMqConnection>.Instance);
        var publisher = new RabbitMqIntegrationEventPublisher(transport);
        AccountDeletionRequestedIntegrationEvent Make(string kind) => new(Guid.NewGuid(), Guid.NewGuid(), 1, DateTime.UtcNow);
        try
        {
            foreach (var kind in new[] { "requested" })
            {
                var message = Make(kind);
                await publisher.PublishAsync(message, default);
                var delivery = await channel.BasicGetAsync(settings.QueueName, autoAck: false);
                check(delivery is not null, "RabbitMQ delivers " + kind);
                var props = delivery!.BasicProperties;
                check(delivery.RoutingKey == AccountDeletionRequestedIntegrationEvent.EventType && props.Type == AccountDeletionRequestedIntegrationEvent.EventType &&
                    props.Persistent && props.ContentType == "application/json", "Routing, type and persistent JSON: " + kind);
                check(props.MessageId == message.EventId.ToString() && props.CorrelationId == message.OperationId.ToString(),
                    "Stable AMQP identity: " + kind);
                check(JsonSerializer.Deserialize<AccountDeletionRequestedIntegrationEvent>(delivery.Body.Span) == message, "Real broker JSON roundtrip: " + kind);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false);
                await publisher.PublishAsync(message, default);
                var repeated = await channel.BasicGetAsync(settings.QueueName, autoAck: true);
                check(repeated?.BasicProperties.MessageId == props.MessageId, "Retry retains EventId: " + kind);
            }
            bool returned = false;
            try { await transport.PublishAsync("unbound.test.v1", Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, new byte[] { 1 }, default); }
            catch (PublishException ex) { returned = ex.IsReturn; }
            check(returned, "Mandatory unroutable publication fails instead of reporting success");
            var recovery = Make("created");
            await publisher.PublishAsync(recovery, default);
            check((await channel.BasicGetAsync(settings.QueueName, true))?.BasicProperties.MessageId == recovery.EventId.ToString(),
                "Transport reconnects after failed publication");
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            bool canceledCorrectly = false;
            try { await publisher.PublishAsync(Make("created"), canceled.Token); }
            catch (OperationCanceledException) { canceledCorrectly = true; }
            check(canceledCorrectly, "Publication respects caller cancellation");
            bool rejected = false;
            try { await publisher.PublishAsync(Make("created") with { Version = 99 }, default); }
            catch (InvalidOperationException) { rejected = true; }
            check(rejected, "Unsupported event version rejected before publishing");
            await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => publisher.PublishAsync(Make("created"), default)));
            var ids = new HashSet<string>();
            for (int i = 0; i < 5; i++) ids.Add((await channel.BasicGetAsync(settings.QueueName, true))!.BasicProperties.MessageId!);
            check(ids.Count == 5, "Concurrent callers publish safely on serialized channel");
        }
        finally
        {
            await channel.QueueDeleteAsync(settings.QueueName, ifUnused: false, ifEmpty: false);
            await channel.ExchangeDeleteAsync(settings.ExchangeName, ifUnused: false);
        }
    }
}