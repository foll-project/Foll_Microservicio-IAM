namespace foll_iam.Shared.Domain.Repositories;

public interface IUnitOfWork
{
    Task CompleteAsync();
}
