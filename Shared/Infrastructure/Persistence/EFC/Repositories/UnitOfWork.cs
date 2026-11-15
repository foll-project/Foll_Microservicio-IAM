using foll_iam.Shared.Domain.Repositories;
using foll_iam.Shared.Infrastructure.Persistence.EFC.Configuration;

namespace foll_iam.Shared.Infrastructure.Persistence.EFC.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context) => _context = context;

    public async Task CompleteAsync()
    {
        try { await _context.SaveChangesAsync(); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (
            ex.InnerException is Npgsql.PostgresException
            { SqlState: "23505", ConstraintName: "ix_account_deletion_requests_user_id" })
        {
            _context.ChangeTracker.Clear();
            throw new foll_iam.IAM.Domain.Exceptions.AccountDeletionConflictException(ex);
        }
    }
}
