using System.Net;
using ServiceRequest.IntegrationTests.TestSupport;

namespace ServiceRequest.IntegrationTests.Authentication;

public sealed class PolicyTests : IDisposable
{
    private readonly ApiTestFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string username, string password)
    {
        var client = _factory.CreateClient();
        await client.AuthenticateAsync(username, password);
        return client;
    }

    [Fact]
    public async Task Employee_DoesNotSatisfy_RequireSupportAgent()
    {
        var client = await CreateAuthenticatedClientAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);

        var response = await client.GetAsync("/api/test/policy-probe/support-agent");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SupportAgent_DoesNotSatisfy_RequireAdmin()
    {
        var client = await CreateAuthenticatedClientAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);

        var response = await client.GetAsync("/api/test/policy-probe/admin");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_Satisfies_RequireAdmin()
    {
        var client = await CreateAuthenticatedClientAsync(ApiTestFactory.AdminUsername, ApiTestFactory.AdminPassword);

        var response = await client.GetAsync("/api/test/policy-probe/admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SupportAgent_Satisfies_CanManageRequests()
    {
        var client = await CreateAuthenticatedClientAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);

        var response = await client.GetAsync("/api/test/policy-probe/manage-requests");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Admin_Satisfies_CanManageRequests()
    {
        var client = await CreateAuthenticatedClientAsync(ApiTestFactory.AdminUsername, ApiTestFactory.AdminPassword);

        var response = await client.GetAsync("/api/test/policy-probe/manage-requests");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Employee_DoesNotSatisfy_CanManageRequests()
    {
        var client = await CreateAuthenticatedClientAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);

        var response = await client.GetAsync("/api/test/policy-probe/manage-requests");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Employee_Satisfies_RequireEmployee()
    {
        var client = await CreateAuthenticatedClientAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);

        var response = await client.GetAsync("/api/test/policy-probe/employee");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
