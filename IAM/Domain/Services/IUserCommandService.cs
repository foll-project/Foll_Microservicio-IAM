using foll_iam.IAM.Domain.Model.Commands;

namespace foll_iam.IAM.Domain.Services;

public interface IUserCommandService
{
    Task Handle(RegisterCommand command);
}
