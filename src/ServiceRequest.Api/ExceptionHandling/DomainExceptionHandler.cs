using Microsoft.AspNetCore.Diagnostics;
using ServiceRequest.Domain.Exceptions;

namespace ServiceRequest.Api.ExceptionHandling;

public sealed class DomainExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;

    public DomainExceptionHandler(IProblemDetailsService problemDetailsService)
    {
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            RequestCategoryNotFoundException => ((int?)StatusCodes.Status404NotFound, "Category not found"),
            DuplicateRequestCategoryNameException => ((int?)StatusCodes.Status409Conflict, "Duplicate category name"),
            InvalidCredentialsException => ((int?)StatusCodes.Status401Unauthorized, "Invalid credentials"),
            _ => (null, null),
        };

        if (statusCode is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = statusCode.Value;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = statusCode,
                Title = title,
                Detail = exception.Message,
            },
        });
    }
}
