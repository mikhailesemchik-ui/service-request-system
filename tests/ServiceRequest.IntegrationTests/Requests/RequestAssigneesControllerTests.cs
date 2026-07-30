using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceRequest.IntegrationTests.TestSupport;

namespace ServiceRequest.IntegrationTests.Requests;

public sealed class RequestAssigneesControllerTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = HttpClientAuthenticationExtensions.JsonOptions;

    private readonly ApiTestFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    public async Task GetAll_WhenAnonymous_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/request-assignees");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WhenEmployee_ReturnsForbidden()
    {
        var client = _factory.CreateClient();
        await client.AuthenticateAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);

        var response = await client.GetAsync("/api/request-assignees");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WhenSupportAgent_ReturnsForbidden()
    {
        var client = _factory.CreateClient();
        await client.AuthenticateAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);

        var response = await client.GetAsync("/api/request-assignees");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WhenAdmin_ReturnsOkWithOnlyActiveSupportStaff()
    {
        var client = _factory.CreateClient();
        await client.AuthenticateAsync(ApiTestFactory.AdminUsername, ApiTestFactory.AdminPassword);

        var response = await client.GetAsync("/api/request-assignees");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var assignees = await response.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOptions);
        Assert.NotNull(assignees);
        Assert.DoesNotContain(assignees!, a => a.GetProperty("role").GetString() == "Employee");
        Assert.Contains(assignees!, a => a.GetProperty("role").GetString() == "SupportAgent");
        Assert.Contains(assignees!, a => a.GetProperty("role").GetString() == "Admin");
    }
}
