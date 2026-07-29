using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ServiceRequest.Application.Authentication;
using ServiceRequest.Domain.Enums;

namespace ServiceRequest.Api.Authentication;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptionsAccessor) =>
            {
                var jwtOptions = jwtOptionsAccessor.Value;

                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };

                bearerOptions.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                        var problemDetailsService = context.HttpContext.RequestServices
                            .GetRequiredService<IProblemDetailsService>();

                        await problemDetailsService.WriteAsync(new ProblemDetailsContext
                        {
                            HttpContext = context.HttpContext,
                            ProblemDetails = new()
                            {
                                Status = StatusCodes.Status401Unauthorized,
                                Title = "Unauthorized",
                                Detail = "Authentication is required to access this resource.",
                            },
                        });
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;

                        var problemDetailsService = context.HttpContext.RequestServices
                            .GetRequiredService<IProblemDetailsService>();

                        await problemDetailsService.WriteAsync(new ProblemDetailsContext
                        {
                            HttpContext = context.HttpContext,
                            ProblemDetails = new()
                            {
                                Status = StatusCodes.Status403Forbidden,
                                Title = "Forbidden",
                                Detail = "You do not have permission to access this resource.",
                            },
                        });
                    },
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireEmployee", policy => policy.RequireRole(nameof(UserRole.Employee)));
            options.AddPolicy("RequireSupportAgent", policy => policy.RequireRole(nameof(UserRole.SupportAgent)));
            options.AddPolicy("RequireAdmin", policy => policy.RequireRole(nameof(UserRole.Admin)));
            options.AddPolicy(
                "CanManageRequests",
                policy => policy.RequireRole(nameof(UserRole.SupportAgent), nameof(UserRole.Admin)));
        });

        return services;
    }
}
