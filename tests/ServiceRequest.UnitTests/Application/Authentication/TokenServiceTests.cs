using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ServiceRequest.Application.Authentication;

namespace ServiceRequest.UnitTests.Application.Authentication;

public class TokenServiceTests
{
    private const string SigningKey = "unit-test-signing-key-that-is-long-enough-1234567890";
    private const string Issuer = "ServiceRequest.Api.UnitTests";
    private const string Audience = "ServiceRequest.Client.UnitTests";

    private static TokenService CreateTokenService(int expirationMinutes = 60) =>
        new(Options.Create(new JwtOptions
        {
            Issuer = Issuer,
            Audience = Audience,
            SigningKey = SigningKey,
            ExpirationMinutes = expirationMinutes,
        }));

    private static AuthenticatedUserDto CreateUser() =>
        new(42, "jane.doe", "Jane Doe", "jane.doe@example.com", "Employee");

    [Fact]
    public void CreateToken_IncludesUserId()
    {
        var tokenService = CreateTokenService();
        var jwt = ReadToken(tokenService.CreateToken(CreateUser()).Token);

        Assert.Equal("42", jwt.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value);
    }

    [Fact]
    public void CreateToken_IncludesUsername()
    {
        var tokenService = CreateTokenService();
        var jwt = ReadToken(tokenService.CreateToken(CreateUser()).Token);

        Assert.Equal("jane.doe", jwt.Claims.Single(c => c.Type == ClaimTypes.Name).Value);
    }

    [Fact]
    public void CreateToken_IncludesDisplayName()
    {
        var tokenService = CreateTokenService();
        var jwt = ReadToken(tokenService.CreateToken(CreateUser()).Token);

        Assert.Equal("Jane Doe", jwt.Claims.Single(c => c.Type == TokenService.DisplayNameClaimType).Value);
    }

    [Fact]
    public void CreateToken_IncludesRole()
    {
        var tokenService = CreateTokenService();
        var jwt = ReadToken(tokenService.CreateToken(CreateUser()).Token);

        Assert.Equal("Employee", jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void CreateToken_SetsCorrectIssuerAndAudience()
    {
        var tokenService = CreateTokenService();
        var jwt = ReadToken(tokenService.CreateToken(CreateUser()).Token);

        Assert.Equal(Issuer, jwt.Issuer);
        Assert.Contains(Audience, jwt.Audiences);
    }

    [Fact]
    public void CreateToken_ExpirationIsInTheFuture()
    {
        var tokenService = CreateTokenService();
        var result = tokenService.CreateToken(CreateUser());

        Assert.True(result.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public void CreateToken_ProducesTokenThatValidatesWithConfiguredKey()
    {
        var tokenService = CreateTokenService();
        var result = tokenService.CreateToken(CreateUser());

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            ValidateLifetime = true,
        };

        var principal = new JwtSecurityTokenHandler().ValidateToken(result.Token, validationParameters, out var validatedToken);

        Assert.NotNull(principal);
        Assert.NotNull(validatedToken);
    }

    private static JwtSecurityToken ReadToken(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);
}
