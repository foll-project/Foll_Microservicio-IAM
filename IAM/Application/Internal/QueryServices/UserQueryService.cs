using foll_iam.IAM.Application.OutboundServices;
using foll_iam.IAM.Domain.Model.Queries;
using foll_iam.IAM.Domain.Repositories;
using foll_iam.IAM.Domain.Services;
using foll_iam.IAM.Domain.Model.Entities;

namespace foll_iam.IAM.Application.Internal.QueryServices;

public class UserQueryService : IUserQueryService
{
    public Task<AccountDeletionRequest?> GetAccountDeletionRequestAsync(long userId) =>
        _userRepository.FindAccountDeletionRequestByUserIdAsync(userId);

    private readonly IUserRepository _userRepository;
    private readonly IHashingService _hashingService;
    private readonly ITokenService _tokenService;

    public UserQueryService(
        IUserRepository userRepository,
        IHashingService hashingService,
        ITokenService tokenService)
    {
        _userRepository = userRepository;
        _hashingService = hashingService;
        _tokenService = tokenService;
    }

    public async Task<(User user, string token)?> AuthenticateAsync(LoginQuery query)
    {
        var user = await _userRepository.FindByEmailAsync(query.Email);
        if (user == null) return null;

        if (!_hashingService.VerifyPassword(query.Password, user.PasswordHash)) return null;

        var token = _tokenService.GenerateToken(user);

        return (user, token);
    }

    public async Task<User?> GetUserInfoByIdAsync(long userId)
    {
        if (userId <= 0) return null;

        var user = await _userRepository.FindByIdAsync(userId);
        if (user is null) return null;

        return user;
    }
}
