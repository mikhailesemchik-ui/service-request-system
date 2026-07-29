using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServiceRequest.Application.Authentication;
using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Exceptions;
using ServiceRequest.Infrastructure.Data;

namespace ServiceRequest.Infrastructure.Authentication;

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;

    public AuthenticationService(ApplicationDbContext dbContext, IPasswordHasher<ApplicationUser> passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    public async Task<AuthenticatedUserDto> LoginAsync(string username, string password, CancellationToken cancellationToken)
    {
        var trimmedUsername = username.Trim();

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(u => u.Username == trimmedUsername, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new InvalidCredentialsException();
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            throw new InvalidCredentialsException();
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var newHash = _passwordHasher.HashPassword(user, password);
            user.SetPasswordHash(newHash);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return ToDto(user);
    }

    private static AuthenticatedUserDto ToDto(ApplicationUser user) =>
        new(user.Id, user.Username, user.DisplayName, user.Email, user.Role.ToString());
}
