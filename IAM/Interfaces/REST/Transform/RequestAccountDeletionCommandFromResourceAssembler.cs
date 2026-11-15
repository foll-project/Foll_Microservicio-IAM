using foll_iam.IAM.Domain.Model.Commands;
using foll_iam.IAM.Interfaces.REST.Resources;

namespace foll_iam.IAM.Interfaces.REST.Transform;

public static class RequestAccountDeletionCommandFromResourceAssembler
{
    public static RequestAccountDeletionCommand ToCommandFromResource(long userId, DeleteAccountResource resource) =>
        new(userId, resource.Password);
}
