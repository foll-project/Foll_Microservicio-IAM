namespace foll_iam.Shared.Infrastructure.Messaging;
public record AccountDeletionRequested(Guid EventId, Guid OperationId, long UserId, DateTime OccurredAtUtc, int Version = 1)
{
    public const string EventType = "iam.account-deletion-requested.v1";
}
public record AccountDeletionCompleted(Guid EventId, Guid OperationId, long UserId, long[] DeletedPatientIds,
    DateTime OccurredAtUtc, int Version = 1)
{
    public const string EventType = "care.account-deletion-completed.v1";
}

