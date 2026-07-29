using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;
using ServiceRequest.Domain.Exceptions;
using ServiceRequest.Infrastructure.Authentication;
using ServiceRequest.Infrastructure.Data;

namespace ServiceRequest.IntegrationTests.Infrastructure.Authentication;

public sealed class AuthenticationServiceTests : IDisposable
{
    private readonly string _databasePath;
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly AuthenticationService _authenticationService;

    public AuthenticationServiceTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"service-requests-auth-tests-{Guid.NewGuid():N}.db");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={_databasePath};Pooling=False")
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.Migrate();

        _passwordHasher = new PasswordHasher<ApplicationUser>();
        _authenticationService = new AuthenticationService(_dbContext, _passwordHasher);
    }

    public void Dispose()
    {
        _dbContext.Dispose();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private async Task<ApplicationUser> CreateUserAsync(string username, string password, bool isActive = true)
    {
        var user = new ApplicationUser(username, "Jane Doe", $"{username}@example.test", UserRole.Employee);
        user.SetPasswordHash(_passwordHasher.HashPassword(user, password));

        if (!isActive)
        {
            user.SetActiveState(false);
        }

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        return user;
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_Succeeds()
    {
        await CreateUserAsync("jane.doe", "Password123!");

        var result = await _authenticationService.LoginAsync("jane.doe", "Password123!", CancellationToken.None);

        Assert.Equal("jane.doe", result.Username);
    }

    [Fact]
    public async Task LoginAsync_TrimsSubmittedUsername()
    {
        await CreateUserAsync("jane.doe", "Password123!");

        var result = await _authenticationService.LoginAsync("  jane.doe  ", "Password123!", CancellationToken.None);

        Assert.Equal("jane.doe", result.Username);
    }

    [Fact]
    public async Task LoginAsync_UsernameMatchingIsCaseInsensitive()
    {
        await CreateUserAsync("jane.doe", "Password123!");

        var result = await _authenticationService.LoginAsync("JANE.DOE", "Password123!", CancellationToken.None);

        Assert.Equal("jane.doe", result.Username);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidUsername_ThrowsInvalidCredentialsException()
    {
        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _authenticationService.LoginAsync("nonexistent-user", "Password123!", CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ThrowsInvalidCredentialsException()
    {
        await CreateUserAsync("jane.doe", "Password123!");

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _authenticationService.LoginAsync("jane.doe", "WrongPassword!", CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_WithInactiveUser_ThrowsInvalidCredentialsException()
    {
        await CreateUserAsync("jane.doe", "Password123!", isActive: false);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _authenticationService.LoginAsync("jane.doe", "Password123!", CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_InvalidUsernameAndInvalidPassword_ProduceTheSamePublicFailure()
    {
        await CreateUserAsync("jane.doe", "Password123!");

        var invalidUsernameException = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _authenticationService.LoginAsync("nonexistent-user", "Password123!", CancellationToken.None));
        var invalidPasswordException = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _authenticationService.LoginAsync("jane.doe", "WrongPassword!", CancellationToken.None));

        Assert.Equal(invalidUsernameException.Message, invalidPasswordException.Message);
    }

    [Fact]
    public async Task LoginAsync_WhenHashNeedsRehash_ReplacesHashAndPersistsUpdate()
    {
        var weakHasher = new PasswordHasher<ApplicationUser>(
            Options.Create(new PasswordHasherOptions { IterationCount = 1 }));

        var user = new ApplicationUser("jane.doe", "Jane Doe", "jane.doe@example.test", UserRole.Employee);
        var weakHash = weakHasher.HashPassword(user, "Password123!");
        user.SetPasswordHash(weakHash);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var result = await _authenticationService.LoginAsync("jane.doe", "Password123!", CancellationToken.None);

        Assert.Equal("jane.doe", result.Username);

        using var verifyContext = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"Data Source={_databasePath};Pooling=False")
                .Options);
        var persistedUser = await verifyContext.Users.SingleAsync(u => u.Username == "jane.doe");

        Assert.NotEqual(weakHash, persistedUser.PasswordHash);

        var verification = _passwordHasher.VerifyHashedPassword(persistedUser, persistedUser.PasswordHash, "Password123!");
        Assert.Equal(PasswordVerificationResult.Success, verification);
    }
}
