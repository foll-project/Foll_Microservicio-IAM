using foll_iam.IAM.Domain.Model.Entities;

namespace foll_iam.IAM.Application.OutboundServices;

public interface ITokenService
{
    string GenerateToken(User user);
}
