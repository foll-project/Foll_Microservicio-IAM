using foll_iam.IAM.Domain.Services;
using foll_iam.IAM.Interfaces.REST.Resources;
using foll_iam.IAM.Interfaces.REST.Transform;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace foll_iam.IAM.Interfaces.REST;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/iam/users")]
public class UserController : ControllerBase
{
    private readonly IUserQueryService _userQueryService;

    public UserController(IUserQueryService userQueryService)
    {
        _userQueryService = userQueryService;
    }

    [HttpGet("{userId:long}")]
    public async Task<IActionResult> GetUserInfoById(long userId)
    {
        var user = await _userQueryService.GetUserInfoByIdAsync(userId);

        if (user == null)
        {
            return NotFound(new { message = "Usuario no encontrado." });
        }

        var resource = new UserResource(
            user.UserId,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PhoneNumber
        );

        return Ok(resource);
    }
}