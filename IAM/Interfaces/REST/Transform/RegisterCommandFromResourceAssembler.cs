using foll_iam.IAM.Domain.Model.Commands;
using foll_iam.IAM.Interfaces.REST.Resources;

namespace foll_iam.IAM.Interfaces.REST.Transform;

public static class RegisterCommandFromResourceAssembler
{
    public static RegisterCommand ToCommandFromResource(RegisterResource resource)
    {
        return new RegisterCommand(resource.Email, resource.Password, resource.FirstName, resource.LastName, resource.PhoneNumber);
    }
}
