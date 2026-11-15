using foll_iam.IAM.Application.IntegrationEvents;

namespace foll_iam.IAM.Application.OutboundServices;

public interface IIntegrationEventPublisher
{
    // Completes only after durable transport acceptance. Failures must throw.
    // Repeated deliveries must retain the original EventId and OperationId.
    Task PublishAsync(AccountDeletionRequestedIntegrationEvent integrationEvent, CancellationToken cancellationToken);
}
