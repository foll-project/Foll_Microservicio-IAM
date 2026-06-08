using foll_iam.IAM.Domain.Model.Entities;
using foll_iam.IAM.Domain.Model.Queries;
using foll_iam.IAM.Interfaces.REST.Resources;

namespace foll_iam.IAM.Interfaces.REST.Transform;

public static class LoginQueryFromRequestAssembler
{
    public static LoginQuery ToQuery(LoginRequest request)
    {
        return new LoginQuery(request.Email, request.Password);
    }

    public static LoginResponse ToResponse(User user, string token)
    {
        return new LoginResponse(user.UserId, user.Email, user.FirstName, user.LastName, user.PhoneNumber, token);
    }
}
