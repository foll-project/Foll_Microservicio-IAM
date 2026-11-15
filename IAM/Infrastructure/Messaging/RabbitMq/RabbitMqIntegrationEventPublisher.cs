using System.Text.Json;
using foll_iam.IAM.Application.IntegrationEvents;
using foll_iam.IAM.Application.OutboundServices;

namespace foll_iam.IAM.Infrastructure.Messaging.RabbitMq;

public sealed class RabbitMqIntegrationEventPublisher(RabbitMqConnection connection) : IIntegrationEventPublisher
{
    public Task PublishAsync(AccountDeletionRequestedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        if (integrationEvent.Version != 1 || integrationEvent.EventId == Guid.Empty ||
            integrationEvent.OperationId == Guid.Empty)
            throw new InvalidOperationException("Evento de integracion invalido o version no soportada.");

        return connection.PublishAsync(AccountDeletionRequestedIntegrationEvent.EventType, integrationEvent.EventId,
            integrationEvent.OperationId, integrationEvent.OccurredAtUtc,
            JsonSerializer.SerializeToUtf8Bytes(integrationEvent), cancellationToken);
    }
}