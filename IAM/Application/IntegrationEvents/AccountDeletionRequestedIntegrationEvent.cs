namespace foll_iam.IAM.Application.IntegrationEvents;

public record AccountDeletionRequestedIntegrationEvent(
    Guid EventId, Guid OperationId, long UserId, DateTime OccurredAtUtc, int Version = 1)
{
    public const string EventType = "iam.account-deletion-requested.v1";
}
