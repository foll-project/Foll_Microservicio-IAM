using foll_iam.IAM.Application.IntegrationEvents;
using foll_iam.IAM.Application.OutboundServices;
using foll_iam.Shared.Infrastructure.Persistence.EFC.Configuration;

namespace foll_iam.IAM.Infrastructure.Messaging.Outbox;

public class OutboxUserEventService(AppDbContext context) : IUserEventService
{
    public async Task AccountDeletionRequestedAsync(AccountDeletionRequestedIntegrationEvent integrationEvent) =>
        await context.IntegrationOutboxMessages.AddAsync(IntegrationOutboxMessage.From(integrationEvent));
}
