using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ServiceRequest.Infrastructure.Data;
using ServiceRequest.IntegrationTests.TestSupport;

namespace ServiceRequest.IntegrationTests.Authentication;

public sealed class AuthControllerTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = HttpClientAuthenticationExtensions.JsonOptions;

    private readonly ApiTestFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    private static string CreateSignedToken(
        int? userId,
        DateTime expires,
        string signingKey = ApiTestFactory.TestSigningKey,
        string issuer = ApiTestFactory.TestIssuer,
        string audience = ApiTestFactory.TestAudience,
        string displayName = "Fake Display Name",
        string role = "Admin")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "someuser"),
            new("display_name", displayName),
            new(ClaimTypes.Role, role),
        };

        if (userId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, expires: expires, signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Login

    [Fact]
    public async Task Login_WithValidEmployeeCredentials_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = ApiTestFactory.EmployeeUsername, password = ApiTestFactory.EmployeePassword },
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidSupportAgentCredentials_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = ApiTestFactory.SupportAgentUsername, password = ApiTestFactory.SupportAgentPassword },
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidAdminCredentials_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = ApiTestFactory.AdminUsername, password = ApiTestFactory.AdminPassword },
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ResponseContainsTokenExpirationAndCorrectRole()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = ApiTestFactory.SupportAgentUsername, password = ApiTestFactory.SupportAgentPassword },
            JsonOptions);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));
        Assert.Equal("Bearer", body.GetProperty("tokenType").GetString());
        Assert.True(body.GetProperty("expiresAt").GetDateTimeOffset() > DateTimeOffset.UtcNow);
        Assert.Equal("SupportAgent", body.GetProperty("user").GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_TokenValidatesCryptographicallyWithConfiguredKey()
    {
        var client = _factory.CreateClient();
        var token = await client.LoginAndGetTokenAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = ApiTestFactory.TestIssuer,
            ValidateAudience = true,
            ValidAudience = ApiTestFactory.TestAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiTestFactory.TestSigningKey)),
            ValidateLifetime = true,
        };

        var principal = new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out var validatedToken);

        Assert.NotNull(principal);
        Assert.NotNull(validatedToken);
    }

    [Fact]
    public async Task Login_WithBlankUsername_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "   ", password = "Password123!" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithBlankPassword_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = ApiTestFactory.EmployeeUsername, password = "   " },
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithInvalidUsername_ReturnsUnauthorizedProblemDetails()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "nonexistent-user", password = "Password123!" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorizedProblemDetails()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = ApiTestFactory.EmployeeUsername, password = "WrongPassword!" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Login_WithInactiveUser_ReturnsSameUnauthorizedResponseAsInvalidCredentials()
    {
        var client = _factory.CreateClient();

        var inactiveResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = ApiTestFactory.InactiveUsername, password = ApiTestFactory.InactivePassword },
            JsonOptions);
        var invalidResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "nonexistent-user", password = "Password123!" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, inactiveResponse.StatusCode);

        var inactiveBody = await inactiveResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var invalidBody = await invalidResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(invalidBody.GetProperty("detail").GetString(), inactiveBody.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Login_UsernameMatchingIsCaseInsensitive()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = ApiTestFactory.EmployeeUsername.ToUpperInvariant(), password = ApiTestFactory.EmployeePassword },
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_UsernameIsTrimmed()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = $"  {ApiTestFactory.EmployeeUsername}  ", password = ApiTestFactory.EmployeePassword },
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_PasswordRemainsCaseSensitive()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = ApiTestFactory.EmployeeUsername, password = ApiTestFactory.EmployeePassword.ToUpperInvariant() },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Current user

    [Fact]
    public async Task Me_WithValidToken_ReturnsCurrentDatabaseUser()
    {
        var client = _factory.CreateClient();
        await client.AuthenticateAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(ApiTestFactory.EmployeeUsername, body.GetProperty("username").GetString());
        Assert.Equal("Employee", body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Me_WithMissingToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Me_WithMalformedToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-jwt");

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithExpiredToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var expiredToken = CreateSignedToken(userId: 1, expires: DateTime.UtcNow.AddMinutes(-10));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithInvalidSignature_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var tokenWithWrongSignature = CreateSignedToken(
            userId: 1,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingKey: "a-completely-different-signing-key-not-matching-the-configured-one");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenWithWrongSignature);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithTokenForNonexistentUser_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var token = CreateSignedToken(userId: 999999, expires: DateTime.UtcNow.AddMinutes(60));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithTokenForInactiveUser_ReturnsUnauthorized()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var inactiveUserId = await dbContext.Users
            .Where(u => u.Username == ApiTestFactory.InactiveUsername)
            .Select(u => u.Id)
            .SingleAsync();

        var client = _factory.CreateClient();
        var token = CreateSignedToken(userId: inactiveUserId, expires: DateTime.UtcNow.AddMinutes(60));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsCurrentDatabaseValuesRatherThanStaleTokenClaims()
    {
        var client = _factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = ApiTestFactory.EmployeeUsername, password = ApiTestFactory.EmployeePassword },
            JsonOptions);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var realUserId = loginBody.GetProperty("user").GetProperty("id").GetInt32();

        var forgedToken = CreateSignedToken(
            userId: realUserId,
            expires: DateTime.UtcNow.AddMinutes(60),
            displayName: "Forged Display Name",
            role: "Admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forgedToken);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Test Employee", body.GetProperty("displayName").GetString());
        Assert.Equal("Employee", body.GetProperty("role").GetString());
    }
}
