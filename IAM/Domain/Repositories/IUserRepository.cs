using foll_iam.IAM.Domain.Model.Entities;
using foll_iam.Shared.Domain.Repositories;

namespace foll_iam.IAM.Domain.Repositories;

public interface IUserRepository : IBaseRepository<User>
{
    Task<bool> ExistsByEmailAsync(string email);
    Task<User?> FindByEmailAsync(string email);
}

// ..