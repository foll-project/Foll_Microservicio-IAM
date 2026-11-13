using foll_iam.IAM.Application.OutboundServices;
using foll_iam.IAM.Domain.Model.Commands;
using foll_iam.IAM.Domain.Model.Entities;
using foll_iam.IAM.Domain.Repositories;
using foll_iam.IAM.Domain.Services;
using foll_iam.Shared.Domain.Repositories;

namespace foll_iam.IAM.Application.Internal.CommandServices;

public class UserCommandService : IUserCommandService
{
    private readonly IUserRepository _userRepository;
    private readonly IHashingService _hashingService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserEventService _userEventService;

    public UserCommandService(
        IUserRepository userRepository,
        IHashingService hashingService,
        IUnitOfWork unitOfWork,
        IUserEventService userEventService)
    {
        _userRepository = userRepository;
        _hashingService = hashingService;
        _unitOfWork = unitOfWork;
        _userEventService = userEventService;
    }

    public async Task<AccountDeletionRequest> Handle(RequestAccountDeletionCommand command)
    {
        var user = await _userRepository.FindByIdAsync(command.UserId);
        if (user is null) throw new InvalidOperationException("Usuario no encontrado.");
        if (!_hashingService.VerifyPassword(command.Password, user.PasswordHash))
            throw new InvalidOperationException("Contrasena incorrecta.");

        var existing = await _userRepository.FindAccountDeletionRequestByUserIdAsync(command.UserId);
        if (existing is not null) return existing;

        var request = AccountDeletionRequest.Create(command.UserId);
        await _userRepository.AddAccountDeletionRequestAsync(request);
        await _userEventService.AccountDeletionRequestedAsync(
            new foll_iam.IAM.Application.IntegrationEvents.AccountDeletionRequestedIntegrationEvent(
                Guid.NewGuid(), request.OperationId, request.UserId, request.RequestedAtUtc));
        try
        {
            await _unitOfWork.CompleteAsync();
        }
        catch (foll_iam.IAM.Domain.Exceptions.AccountDeletionConflictException)
        {
            // A concurrent request won the unique constraint. Return its operation.
            return await _userRepository.FindAccountDeletionRequestByUserIdAsync(command.UserId)
                ?? throw new InvalidOperationException("No se pudo recuperar la solicitud existente.");
        }
        return request;
    }

    public async Task Handle(RegisterCommand command)
    {
        if (await _userRepository.ExistsByEmailAsync(command.Email))
        {
            throw new Exception($"El correo {command.Email} ya se encuentra registrado.");
        }

        var hashedPassword = _hashingService.HashPassword(command.Password);

        var user = new User(command.Email, hashedPassword, command.FirstName, command.LastName, command.PhoneNumber);

        await _userRepository.AddAsync(user);

        await _unitOfWork.CompleteAsync();
    }
}
