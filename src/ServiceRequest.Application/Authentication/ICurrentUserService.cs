using System.Security.Claims;

namespace ServiceRequest.Application.Authentication;

public interface ICurrentUserService
{
    Task<AuthenticatedUserDto?> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}
