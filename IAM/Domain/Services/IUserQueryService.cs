using foll_iam.IAM.Domain.Model.Entities;
using foll_iam.IAM.Domain.Model.Queries;

namespace foll_iam.IAM.Domain.Services;

public interface IUserQueryService
{
    Task<(User user, string token)?> AuthenticateAsync(LoginQuery query);

    Task<User?> GetUserInfoByIdAsync(long userId);
}
