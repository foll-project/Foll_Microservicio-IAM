using System.Text.Json;
using foll_iam.IAM.Application.IntegrationEvents;
using foll_iam.IAM.Application.OutboundServices;
using foll_iam.IAM.Infrastructure.Messaging.Configuration;
using foll_iam.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace foll_iam.IAM.Infrastructure.Messaging.Outbox;

public class OutboxPublisherBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<IntegrationMessagingOptions> options,
    ILogger<OutboxPublisherBackgroundService> logger) : BackgroundService
{
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // Fail fast rather than silently claiming publication without a transport.
        if (options.Value.Enabled)
        {
            using var scope = scopeFactory.CreateScope();
            if (scope.ServiceProvider.GetService<IIntegrationEventPublisher>() is null)
                throw new InvalidOperationException("IntegrationMessaging esta habilitado sin un adaptador IIntegrationEventPublisher.");
        }
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Publicacion deshabilitada. Los eventos permanecen pendientes en el outbox.");
            return;
        }
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PublishBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Error procesando el outbox de IAM."); }
            try { await Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task PublishBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
        // Serializes dispatchers across replicas. At-least-once delivery still requires consumer deduplication.
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var messages = await context.IntegrationOutboxMessages.FromSqlInterpolated($"SELECT * FROM iam.integration_outbox_messages WHERE published_at_utc IS NULL AND next_attempt_at_utc <= {DateTime.UtcNow} ORDER BY occurred_at_utc LIMIT {options.Value.BatchSize} FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);
        foreach (var message in messages)
        {
            var request = await context.AccountDeletionRequests.SingleAsync(
                r => r.OperationId == message.OperationId, cancellationToken);
            try
            {
                if (message.EventType != AccountDeletionRequestedIntegrationEvent.EventType || message.Version != 1)
                    throw new InvalidOperationException("Contrato de evento no soportado.");
                var integrationEvent = JsonSerializer.Deserialize<AccountDeletionRequestedIntegrationEvent>(message.Payload)
                    ?? throw new InvalidOperationException("Evento vacio en outbox.");
                await publisher.PublishAsync(integrationEvent, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                message.RecordFailure(DateTime.UtcNow, ex.Message);
                logger.LogWarning(ex, "Fallo publicacion del evento {EventId}; se reintentara.", message.EventId);
                continue;
            }
            var now = DateTime.UtcNow;
            message.MarkAsPublished(now);
            request.MarkAsPublished(now);
        }
        // Persist delivery and business status together. A failed commit causes safe redelivery.
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
