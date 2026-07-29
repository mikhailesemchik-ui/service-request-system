using System.Net;
using System.Net.Http.Json;
using ServiceRequest.IntegrationTests.TestSupport;

namespace ServiceRequest.IntegrationTests.Categories;

public sealed class CategoriesAuthorizationTests : IDisposable
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = HttpClientAuthenticationExtensions.JsonOptions;

    private readonly ApiTestFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    public async Task GetAll_WhenAnonymous_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetAll_WhenAuthenticatedAsEmployee_ReturnsOk()
    {
        var client = _factory.CreateClient();
        await client.AuthenticateAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);

        var response = await client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WhenAuthenticatedAsSupportAgent_ReturnsOk()
    {
        var client = _factory.CreateClient();
        await client.AuthenticateAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);

        var response = await client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WhenAuthenticatedAsEmployee_ReturnsOk()
    {
        var adminClient = _factory.CreateClient();
        await adminClient.AuthenticateAsync(ApiTestFactory.AdminUsername, ApiTestFactory.AdminPassword);
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/categories",
            new { name = "Hardware", description = (string?)null },
            JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(JsonOptions);
        var categoryId = created.GetProperty("id").GetInt32();

        var employeeClient = _factory.CreateClient();
        await employeeClient.AuthenticateAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);

        var response = await employeeClient.GetAsync($"/api/categories/{categoryId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_WhenAuthenticatedAsEmployee_ReturnsForbidden()
    {
        var client = _factory.CreateClient();
        await client.AuthenticateAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);

        var response = await client.PostAsJsonAsync(
            "/api/categories",
            new { name = "Hardware", description = (string?)null },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WhenAuthenticatedAsSupportAgent_ReturnsForbidden()
    {
        var client = _factory.CreateClient();
        await client.AuthenticateAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);

        var response = await client.PostAsJsonAsync(
            "/api/categories",
            new { name = "Hardware", description = (string?)null },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_WhenAuthenticatedAsEmployee_ReturnsForbidden()
    {
        var adminClient = _factory.CreateClient();
        await adminClient.AuthenticateAsync(ApiTestFactory.AdminUsername, ApiTestFactory.AdminPassword);
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/categories",
            new { name = "Hardware", description = (string?)null },
            JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(JsonOptions);
        var categoryId = created.GetProperty("id").GetInt32();

        var employeeClient = _factory.CreateClient();
        await employeeClient.AuthenticateAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);

        var response = await employeeClient.PutAsJsonAsync(
            $"/api/categories/{categoryId}",
            new { name = "Hardware Support", description = (string?)null },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_WhenAuthenticatedAsSupportAgent_ReturnsForbidden()
    {
        var adminClient = _factory.CreateClient();
        await adminClient.AuthenticateAsync(ApiTestFactory.AdminUsername, ApiTestFactory.AdminPassword);
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/categories",
            new { name = "Hardware", description = (string?)null },
            JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(JsonOptions);
        var categoryId = created.GetProperty("id").GetInt32();

        var agentClient = _factory.CreateClient();
        await agentClient.AuthenticateAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);

        var response = await agentClient.PutAsJsonAsync(
            $"/api/categories/{categoryId}",
            new { name = "Hardware Support", description = (string?)null },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SetActiveState_WhenAuthenticatedAsEmployee_ReturnsForbidden()
    {
        var adminClient = _factory.CreateClient();
        await adminClient.AuthenticateAsync(ApiTestFactory.AdminUsername, ApiTestFactory.AdminPassword);
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/categories",
            new { name = "Hardware", description = (string?)null },
            JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(JsonOptions);
        var categoryId = created.GetProperty("id").GetInt32();

        var employeeClient = _factory.CreateClient();
        await employeeClient.AuthenticateAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);

        var response = await employeeClient.PatchAsJsonAsync(
            $"/api/categories/{categoryId}/active-state",
            new { isActive = false },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SetActiveState_WhenAuthenticatedAsSupportAgent_ReturnsForbidden()
    {
        var adminClient = _factory.CreateClient();
        await adminClient.AuthenticateAsync(ApiTestFactory.AdminUsername, ApiTestFactory.AdminPassword);
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/categories",
            new { name = "Hardware", description = (string?)null },
            JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(JsonOptions);
        var categoryId = created.GetProperty("id").GetInt32();

        var agentClient = _factory.CreateClient();
        await agentClient.AuthenticateAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);

        var response = await agentClient.PatchAsJsonAsync(
            $"/api/categories/{categoryId}/active-state",
            new { isActive = false },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanAccessEveryCategoryEndpoint()
    {
        var client = _factory.CreateClient();
        await client.AuthenticateAsync(ApiTestFactory.AdminUsername, ApiTestFactory.AdminPassword);

        var createResponse = await client.PostAsJsonAsync(
            "/api/categories",
            new { name = "Hardware", description = (string?)null },
            JsonOptions);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(JsonOptions);
        var categoryId = created.GetProperty("id").GetInt32();

        var listResponse = await client.GetAsync("/api/categories");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/categories/{categoryId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/categories/{categoryId}",
            new { name = "Hardware Support", description = (string?)null },
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var activeStateResponse = await client.PatchAsJsonAsync(
            $"/api/categories/{categoryId}/active-state",
            new { isActive = false },
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, activeStateResponse.StatusCode);
    }
}
