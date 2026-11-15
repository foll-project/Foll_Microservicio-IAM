using foll_iam.IAM.Domain.Model.Enums;

namespace foll_iam.IAM.Domain.Model.Entities;

public class AccountDeletionRequest
{
    public Guid OperationId { get; private set; }
    public long UserId { get; private set; }
    public AccountDeletionStatus Status { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    private AccountDeletionRequest() { }

    public static AccountDeletionRequest Create(long userId)
    {
        if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
        return new AccountDeletionRequest
        {
            OperationId = Guid.NewGuid(), UserId = userId,
            Status = AccountDeletionStatus.PendingPublication, RequestedAtUtc = DateTime.UtcNow
        };
    }

    public void MarkAsPublished(DateTime publishedAtUtc)
    {
        if (Status is AccountDeletionStatus.WaitingForConfirmations or AccountDeletionStatus.Completed) return;
        if (publishedAtUtc.Kind != DateTimeKind.Utc || publishedAtUtc < RequestedAtUtc)
            throw new ArgumentException("La fecha de publicacion debe ser UTC y posterior a la solicitud.", nameof(publishedAtUtc));
        PublishedAtUtc = publishedAtUtc;
        Status = AccountDeletionStatus.WaitingForConfirmations;
    }

    public void MarkAsCompleted(DateTime completedAtUtc)
    {
        if (Status == AccountDeletionStatus.Completed) return;
        if (completedAtUtc.Kind != DateTimeKind.Utc || completedAtUtc < RequestedAtUtc)
            throw new ArgumentException("La fecha de finalizacion debe ser UTC y posterior a la solicitud.");
        CompletedAtUtc = completedAtUtc;
        Status = AccountDeletionStatus.Completed;
    }
}
