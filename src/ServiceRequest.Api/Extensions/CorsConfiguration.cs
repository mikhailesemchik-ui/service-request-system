using Microsoft.AspNetCore.Cors.Infrastructure;

namespace ServiceRequest.Api.Extensions;

public static class CorsConfiguration
{
    public const string PolicyName = "FrontendClient";

    public static void Configure(CorsOptions options, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        options.AddPolicy(PolicyName, policy =>
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    }
}
