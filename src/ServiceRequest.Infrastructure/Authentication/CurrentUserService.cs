using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ServiceRequest.Application.Authentication;
using ServiceRequest.Infrastructure.Data;

namespace ServiceRequest.Infrastructure.Authentication;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly ApplicationDbContext _dbContext;

    public CurrentUserService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthenticatedUserDto?> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var idClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(idClaim, out var userId))
        {
            return null;
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        return new AuthenticatedUserDto(user.Id, user.Username, user.DisplayName, user.Email, user.Role.ToString());
    }
}
