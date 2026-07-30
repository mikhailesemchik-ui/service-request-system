using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceRequest.IntegrationTests.TestSupport;

namespace ServiceRequest.IntegrationTests.Requests;

public sealed class RequestsHistoryTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = HttpClientAuthenticationExtensions.JsonOptions;

    private readonly ApiTestFactory _factory = new();
    private HttpClient _adminClient = null!;

    public async Task InitializeAsync()
    {
        _adminClient = _factory.CreateClient();
        await _adminClient.AuthenticateAsync(ApiTestFactory.AdminUsername, ApiTestFactory.AdminPassword);
    }

    public Task DisposeAsync()
    {
        _adminClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private async Task<int> CreateCategoryAsync(string name)
    {
        var response = await _adminClient.PostAsJsonAsync(
            "/api/categories", new { name, description = (string?)null }, JsonOptions);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetInt32();
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string username, string password)
    {
        var client = _factory.CreateClient();
        await client.AuthenticateAsync(username, password);
        return client;
    }

    private async Task<int> CreateRequestAsync(HttpClient client, int categoryId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/requests",
            new { title = "Printer not working", description = "The office printer jams every time it is used.", categoryId, priority = "Medium" },
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task GetHistory_WhenAnonymous_ReturnsUnauthorized()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_adminClient, categoryId);
        var anonymousClient = _factory.CreateClient();

        var response = await anonymousClient.GetAsync($"/api/requests/{requestId}/history");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetHistory_WhenEmpty_ReturnsOkWithEmptyArray()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_adminClient, categoryId);

        var response = await _adminClient.GetAsync($"/api/requests/{requestId}/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOptions);
        Assert.NotNull(body);
        Assert.Empty(body!);
    }

    [Fact]
    public async Task GetHistory_AfterStatusChange_ReturnsEntryWithFriendlyValues()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var employeeClient = await CreateAuthenticatedClientAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);
        var requestId = await CreateRequestAsync(employeeClient, categoryId);
        await employeeClient.PatchAsJsonAsync($"/api/requests/{requestId}/status", new { status = "Cancelled" }, JsonOptions);

        var response = await employeeClient.GetAsync($"/api/requests/{requestId}/history");

        var body = await response.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOptions);
        Assert.NotNull(body);
        var entry = Assert.Single(body!);
        Assert.Equal("StatusChanged", entry.GetProperty("action").GetString());
        Assert.Equal("New", entry.GetProperty("previousValue").GetString());
        Assert.Equal("Cancelled", entry.GetProperty("newValue").GetString());
    }

    [Fact]
    public async Task GetHistory_WhenEmployeeRequestsAnotherUsersHistory_ReturnsNotFound()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_adminClient, categoryId);
        var employeeClient = await CreateAuthenticatedClientAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);

        var response = await employeeClient.GetAsync($"/api/requests/{requestId}/history");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetHistory_SupportAgentCanAccessAnyRequestsHistory_ReturnsOk()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_adminClient, categoryId);
        var agentClient = await CreateAuthenticatedClientAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);

        var response = await agentClient.GetAsync($"/api/requests/{requestId}/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetHistory_WithInvalidRequestId_ReturnsBadRequest()
    {
        var response = await _adminClient.GetAsync("/api/requests/0/history");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetHistory_WithMissingRequest_ReturnsNotFound()
    {
        var response = await _adminClient.GetAsync("/api/requests/999999/history");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
