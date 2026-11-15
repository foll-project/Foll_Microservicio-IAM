using foll_iam.IAM.Domain.Services;
using foll_iam.Shared.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace foll_iam.IAM.Interfaces.Internal;

[ApiController]
[Route("internal/iam/users")]
public class NotificationRecipientController(IUserQueryService users) : ControllerBase
{
    [HttpGet("{userId:long}/notification-recipient")]
    [BackgroundQueryKey]
    public async Task<IActionResult> Get(long userId)
    {
        if (userId <= 0) return NotFound();
        var user = await users.GetUserInfoByIdAsync(userId);
        return user is null ? NotFound() : Ok(new { user.UserId, user.FirstName, user.LastName, user.PhoneNumber });
    }
}
