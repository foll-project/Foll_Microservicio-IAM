using foll_iam.IAM.Domain.Services;
using foll_iam.IAM.Interfaces.REST.Resources;
using foll_iam.IAM.Interfaces.REST.Transform;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
 
namespace foll_iam.IAM.Interfaces.REST;

[ApiController]
[Route("api/iam/auth")]
public class AuthenticationController : ControllerBase
{
    private readonly IUserCommandService _userCommandService;
    private readonly IUserQueryService _userQueryService;

    public AuthenticationController(IUserCommandService userCommandService, IUserQueryService userQueryService)
    {
        _userCommandService = userCommandService;
        _userQueryService = userQueryService;
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpDelete]
    public async Task<IActionResult> RequestAccountDeletion([FromBody] DeleteAccountResource resource)
    {
        if (!long.TryParse(User.FindFirst("userId")?.Value, out var userId) || userId <= 0)
            return Unauthorized(new { message = "JWT invalido: userId no encontrado." });
        try
        {
            var request = await _userCommandService.Handle(
                RequestAccountDeletionCommandFromResourceAssembler.ToCommandFromResource(userId, resource));
            return AcceptedAtAction(nameof(GetAccountDeletionRequest),
                AccountDeletionResourceFromEntityAssembler.ToResourceFromEntity(request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpGet("account-deletion")]
    public async Task<IActionResult> GetAccountDeletionRequest()
    {
        if (!long.TryParse(User.FindFirst("userId")?.Value, out var userId) || userId <= 0)
            return Unauthorized(new { message = "JWT invalido: userId no encontrado." });
        var request = await _userQueryService.GetAccountDeletionRequestAsync(userId);
        return request is null
            ? NotFound(new { message = "No existe una solicitud de eliminacion." })
            : Ok(AccountDeletionResourceFromEntityAssembler.ToResourceFromEntity(request));
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterResource resource)
    {
        var command = RegisterCommandFromResourceAssembler.ToCommandFromResource(resource);

        try
        {
            await _userCommandService.Handle(command);
            return Ok(new { message = "Usuario registrado exitosamente." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var query = LoginQueryFromRequestAssembler.ToQuery(request);

        var auth = await _userQueryService.AuthenticateAsync(query);

        if (auth == null)
        {
            return Unauthorized(new { message = "Credenciales inválidas." });
        }

        var (user, token) = auth.Value;

        var response = LoginQueryFromRequestAssembler.ToResponse(user, token);

        return Ok(response);
    }
}
