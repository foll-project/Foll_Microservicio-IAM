using foll_iam.IAM.Domain.Model.Entities;
using foll_iam.IAM.Interfaces.REST.Resources;

namespace foll_iam.IAM.Interfaces.REST.Transform;

public static class AccountDeletionResourceFromEntityAssembler
{
    public static AccountDeletionResource ToResourceFromEntity(AccountDeletionRequest request) =>
        new(request.OperationId, request.Status.ToString(), request.RequestedAtUtc, request.PublishedAtUtc);
}
