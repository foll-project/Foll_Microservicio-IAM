namespace foll_iam.IAM.Interfaces.REST.Resources;

public record AccountDeletionResource(
    Guid OperationId, string Status, DateTime RequestedAtUtc, DateTime? PublishedAtUtc, DateTime? CompletedAtUtc = null);
