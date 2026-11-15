using foll_iam.IAM.Application.IntegrationEvents;

namespace foll_iam.IAM.Application.OutboundServices;

public interface IUserEventService
{
    // Stages the message in the current unit of work; does not publish or commit.
    Task AccountDeletionRequestedAsync(AccountDeletionRequestedIntegrationEvent integrationEvent);
}
