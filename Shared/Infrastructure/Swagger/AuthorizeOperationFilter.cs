using Microsoft.AspNetCore.Authorization;
using foll_iam.Shared.Infrastructure.Security;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace foll_iam.Shared.Infrastructure.Swagger;

public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;

        var allowAnonymous = metadata.OfType<AllowAnonymousAttribute>().Any();

        if (allowAnonymous)
        {
            return;
        }

        var hasAuthorize = metadata.OfType<IAuthorizeData>().Any();

        var hasBackgroundKey = metadata.OfType<BackgroundQueryKeyAttribute>().Any();

        if (!hasAuthorize && !hasBackgroundKey)
        {
            return;
        }

        operation.Responses ??= [];

        if (!operation.Responses.ContainsKey("401"))
        {
            operation.Responses.Add("401", new OpenApiResponse
            {
                Description = "Unauthorized"
            });
        }

        if (!operation.Responses.ContainsKey("403"))
        {
            operation.Responses.Add("403", new OpenApiResponse
            {
                Description = "Forbidden"
            });
        }

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = hasBackgroundKey ? "BackgroundQueryKey" : "Bearer"
                        }
                    }
                ] = []
            }
        ];
    }
}