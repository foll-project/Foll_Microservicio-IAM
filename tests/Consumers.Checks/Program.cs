using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using RabbitMQ.Client;
using foll_iam.Shared.Infrastructure.Messaging;
using AppDbContext = foll_iam.Shared.Infrastructure.Persistence.EFC.Configuration.AppDbContext;

var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json").AddEnvironmentVariables().Build();
var original = new NpgsqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection"));
var dbName = "foll_checks_consumers_" + Guid.NewGuid().ToString("N");
var adminSettings = new NpgsqlConnectionStringBuilder(original.ConnectionString) { Database = "postgres", Pooling = false };
var testSettings = new NpgsqlConnectionStringBuilder(original.ConnectionString) { Database = dbName, Pooling = false };
await using var admin = new NpgsqlConnection(adminSettings.ConnectionString);
await admin.OpenAsync();
await using (var create = new NpgsqlCommand($"CREATE DATABASE {dbName}", admin)) await create.ExecuteNonQueryAsync();
var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(testSettings.ConnectionString).UseSnakeCaseNamingConvention().Options;
var count = 0;
void Check(bool ok, string description)
{
    if (!ok) throw new Exception("FAIL: " + description);
    Console.WriteLine("PASS: " + description);
    count++;
}
try
{
    await using (var db = new AppDbContext(options))
    {
        await db.Database.MigrateAsync();
    }
    BrokerEnvelope envelope;
    await using (var db = new AppDbContext(options))
    {
        envelope = await Fixture.SeedAsync(db);
        await db.SaveChangesAsync();
    }
    await using (var db = new AppDbContext(options))
    {
        Check(await new InboxProcessor(db, Fixture.Handler(db)).ProcessAsync(envelope, default), "real handler commits");
    }
    await using (var db = new AppDbContext(options))
    {
        await Fixture.AssertAsync(db, Check);
        Check(await db.Set<IntegrationInboxMessage>().CountAsync() == 1, "inbox saved with business changes");
        Check(!await new InboxProcessor(db, Fixture.Handler(db)).ProcessAsync(envelope, default), "duplicate ignored across contexts");
    }
    // Failure after database writes must roll back the changes and the inbox receipt.
    var failed = envelope with { EventId = Guid.NewGuid(), OperationId = Guid.NewGuid() };
    try
    {
        await using var db = new AppDbContext(options);
        await new InboxProcessor(db, new FailingHandler(db)).ProcessAsync(failed, default);
        throw new Exception("Expected failure.");
    }
    catch (InjectedFailure) { }
    await using (var db = new AppDbContext(options))
    {
        Check(!await db.Set<IntegrationInboxMessage>().AnyAsync(x => x.EventId == failed.EventId), "failed transaction leaves no receipt or partial writes");
    }
    try
    {
        BrokerEnvelope.Parse(envelope.EventType, Guid.NewGuid().ToString(), envelope.OperationId.ToString(),
            envelope.EventType, Encoding.UTF8.GetBytes(envelope.Payload));
        throw new Exception("Expected metadata rejection.");
    }
    catch (InvalidDataException) { Check(true, "mismatched transport identifiers rejected"); }
    try
    {
        var invalidPayload = envelope.Payload.Replace("\"Version\":1", "\"Version\":2");
        BrokerEnvelope.Parse(envelope.EventType, envelope.EventId.ToString(), envelope.OperationId.ToString(),
            envelope.EventType, Encoding.UTF8.GetBytes(invalidPayload));
        throw new Exception("Expected version rejection.");
    }
    catch (InvalidDataException) { Check(true, "unsupported version rejected"); }

    if (args.Contains("--rabbitmq"))
    {
        var settings = config.GetSection("RabbitMq").Get<BrokerSettings>()!;
        settings.ExchangeName = "foll.checks.consumers." + Guid.NewGuid().ToString("N");
        var queue = settings.ExchangeName;
        var factory = new ConnectionFactory { HostName = settings.HostName, Port = settings.Port,
            UserName = settings.UserName, Password = settings.Password, VirtualHost = settings.VirtualHost };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(true, true));
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Critical));
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(testSettings.ConnectionString).UseSnakeCaseNamingConvention());
        var attempts = new RetryProbe();
        services.AddScoped<IBrokerMessageHandler>(sp => new FailOnceHandler(
            Fixture.Handler(sp.GetRequiredService<AppDbContext>()), attempts));
        services.AddScoped<InboxProcessor>();
        services.AddSingleton(new ConsumerSubscription(settings, queue, new[] { envelope.EventType }, true));
        services.AddSingleton<RabbitMqConsumer>();
        await using var provider = services.BuildServiceProvider();
        var consumer = provider.GetRequiredService<RabbitMqConsumer>();
        try
        {
            await consumer.StartAsync(default);
            for (var i = 0; i < 100; i++)
            {
                await Task.Delay(100);
                // The consumer declares topology before this connection publishes.
                var queues = await channel.QueueDeclareAsync(queue, true, false, false);
                if (queues is not null) break;
            }
            await channel.ExchangeDeclareAsync(settings.ExchangeName, ExchangeType.Topic, true, false);
            await channel.QueueBindAsync(queue, settings.ExchangeName, envelope.EventType);
            BrokerEnvelope fresh;
            await using (var db = new AppDbContext(options))
            {
                fresh = await Fixture.SeedAsync(db);
                await db.SaveChangesAsync();
            }
            var props = new BasicProperties { Persistent = true, ContentType = "application/json",
                MessageId = fresh.EventId.ToString(), CorrelationId = fresh.OperationId.ToString(), Type = fresh.EventType };
            await channel.BasicPublishAsync(settings.ExchangeName, fresh.EventType, true, props, Encoding.UTF8.GetBytes(fresh.Payload));
            await channel.BasicPublishAsync(settings.ExchangeName, fresh.EventType, true, props, Encoding.UTF8.GetBytes(fresh.Payload));
            var poisonId = Guid.NewGuid();
            var badProps = new BasicProperties { Persistent = true, MessageId = poisonId.ToString(),
                CorrelationId = Guid.NewGuid().ToString(), Type = fresh.EventType };
            await channel.BasicPublishAsync(settings.ExchangeName, fresh.EventType, true, badProps, Encoding.UTF8.GetBytes("{broken"));
            var received = false;
            for (var i = 0; i < 150; i++)
            {
                await using var db = new AppDbContext(options);
                received = await db.Set<IntegrationInboxMessage>().AnyAsync(x => x.EventId == fresh.EventId);
                var ready = await channel.QueueDeclarePassiveAsync(queue);
                if (received && ready.MessageCount == 0)
                {
                    var deadStatus = await channel.QueueDeclarePassiveAsync(queue + ".dead");
                    if (deadStatus.MessageCount == 1) break;
                }
                await Task.Delay(100);
            }
            Check(received, "RabbitMQ consumer processes a real event");
            Check(attempts.Attempts >= 2, "transient handler failure retried through the broker consumer");
            await consumer.StopAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
            await using (var db = new AppDbContext(options))
                Check(await db.Set<IntegrationInboxMessage>().CountAsync(x => x.EventId == fresh.EventId) == 1, "broker redelivery deduplicated");
            var dead = await channel.BasicGetAsync(queue + ".dead", true);
            Check(dead is not null && dead.BasicProperties.MessageId == poisonId.ToString() &&
                Encoding.UTF8.GetString(dead.Body.Span) == "{broken", "malformed event quarantined durably without changing payload");
        }
        finally
        {
            await consumer.StopAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
            await channel.QueueDeleteAsync(queue, false, false);
            await channel.QueueDeleteAsync(queue + ".dead", false, false);
            await channel.ExchangeDeleteAsync(settings.ExchangeName, false);
        }
    }
    Console.WriteLine($"Passed {count} consumer checks.");
}
finally
{
    // Only the database name generated by this test can be removed.
    if (!dbName.StartsWith("foll_checks_consumers_") || dbName.Length != 54) throw new Exception("Unsafe test database name.");
    await using var drop = new NpgsqlCommand($"DROP DATABASE {dbName} WITH (FORCE)", admin);
    await drop.ExecuteNonQueryAsync();
}

sealed class InjectedFailure : Exception { }
sealed class RetryProbe { public int Attempts; }
sealed class FailOnceHandler(IBrokerMessageHandler inner, RetryProbe probe) : IBrokerMessageHandler
{
    public Task HandleAsync(BrokerEnvelope message, CancellationToken ct)
    {
        if (Interlocked.Increment(ref probe.Attempts) == 1) throw new InjectedFailure();
        return inner.HandleAsync(message, ct);
    }
}
sealed class FailingHandler(AppDbContext db) : IBrokerMessageHandler
{
    public async Task HandleAsync(BrokerEnvelope envelope, CancellationToken ct)
    {
        db.Add(new IntegrationInboxMessage { EventId = envelope.EventId, OperationId = envelope.OperationId,
            EventType = envelope.EventType, Payload = envelope.Payload, ProcessedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        throw new InjectedFailure();
    }
}

