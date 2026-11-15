using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace foll_iam.Shared.Infrastructure.Security;

[AttributeUsage(AttributeTargets.Method)]
public sealed class BackgroundQueryKeyAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expected = configuration["InternalApi:BackgroundQueryKey"];
        var supplied = context.HttpContext.Request.Headers["X-Foll-Service-Key"];
        if (string.IsNullOrWhiteSpace(expected) || expected.Length < 32 || supplied.Count != 1 ||
            string.IsNullOrWhiteSpace(supplied[0]) ||
            !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
                SHA256.HashData(Encoding.UTF8.GetBytes(supplied[0]!))))
            context.Result = new UnauthorizedResult();
    }
}
