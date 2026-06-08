using foll_iam.IAM.Domain.Model.Entities;
using foll_iam.IAM.Domain.Repositories;
using foll_iam.Shared.Infrastructure.Persistence.EFC.Configuration;
using foll_iam.Shared.Infrastructure.Persistence.EFC.Repositories;
using Microsoft.EntityFrameworkCore;

namespace foll_iam.IAM.Infrastructure.Persistence.EFC.Repositories;

public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<bool> ExistsByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var normalized = email.Trim().ToLowerInvariant();
        return await Context.Users.AnyAsync(u => u.Email == normalized);
    }

    public async Task<User?> FindByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var normalized = email.Trim().ToLowerInvariant();
        return await Context.Users.FirstOrDefaultAsync(u => u.Email == normalized);
    }
}
