using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceRequest.IntegrationTests.TestSupport;

namespace ServiceRequest.IntegrationTests.Requests;

public sealed class RequestsAssignmentTests : IAsyncLifetime
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

    private static async Task<int> GetUserIdAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/me");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task SetAssignment_WhenAnonymous_ReturnsUnauthorized()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_adminClient, categoryId);
        var anonymousClient = _factory.CreateClient();

        var response = await anonymousClient.PatchAsJsonAsync(
            $"/api/requests/{requestId}/assignment", new { assignedToUserId = (int?)null }, JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SetAssignment_WhenEmployee_ReturnsForbidden()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var employeeClient = await CreateAuthenticatedClientAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);
        var requestId = await CreateRequestAsync(employeeClient, categoryId);
        var agentId = await GetUserIdAsync(await CreateAuthenticatedClientAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword));

        var response = await employeeClient.PatchAsJsonAsync(
            $"/api/requests/{requestId}/assignment", new { assignedToUserId = agentId }, JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task SetAssignment_SupportAgentAssignsToSelf_ReturnsOkWithUpdatedAssignee()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var employeeClient = await CreateAuthenticatedClientAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);
        var requestId = await CreateRequestAsync(employeeClient, categoryId);
        var agentClient = await CreateAuthenticatedClientAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);
        var agentId = await GetUserIdAsync(agentClient);

        var response = await agentClient.PatchAsJsonAsync(
            $"/api/requests/{requestId}/assignment", new { assignedToUserId = agentId }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(agentId, body.GetProperty("assignedTo").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task SetAssignment_SupportAgentAssignsToAnotherUser_ReturnsForbidden()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var employeeClient = await CreateAuthenticatedClientAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);
        var requestId = await CreateRequestAsync(employeeClient, categoryId);
        var agentClient = await CreateAuthenticatedClientAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);
        var otherAgentClient = await CreateAuthenticatedClientAsync(ApiTestFactory.SecondSupportAgentUsername, ApiTestFactory.SecondSupportAgentPassword);
        var otherAgentId = await GetUserIdAsync(otherAgentClient);

        var response = await agentClient.PatchAsJsonAsync(
            $"/api/requests/{requestId}/assignment", new { assignedToUserId = otherAgentId }, JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SetAssignment_SupportAgentTriesToTakeOverAnotherAgentsRequest_ReturnsForbidden()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var employeeClient = await CreateAuthenticatedClientAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);
        var requestId = await CreateRequestAsync(employeeClient, categoryId);
        var agentClient = await CreateAuthenticatedClientAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);
        var otherAgentClient = await CreateAuthenticatedClientAsync(ApiTestFactory.SecondSupportAgentUsername, ApiTestFactory.SecondSupportAgentPassword);
        var otherAgentId = await GetUserIdAsync(otherAgentClient);
        await otherAgentClient.PatchAsJsonAsync($"/api/requests/{requestId}/assignment", new { assignedToUserId = otherAgentId }, JsonOptions);

        var agentId = await GetUserIdAsync(agentClient);
        var response = await agentClient.PatchAsJsonAsync(
            $"/api/requests/{requestId}/assignment", new { assignedToUserId = agentId }, JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SetAssignment_SupportAgentRemovesOwnAssignment_ReturnsOkWithNullAssignee()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var employeeClient = await CreateAuthenticatedClientAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);
        var requestId = await CreateRequestAsync(employeeClient, categoryId);
        var agentClient = await CreateAuthenticatedClientAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);
        var agentId = await GetUserIdAsync(agentClient);
        await agentClient.PatchAsJsonAsync($"/api/requests/{requestId}/assignment", new { assignedToUserId = agentId }, JsonOptions);

        var response = await agentClient.PatchAsJsonAsync(
            $"/api/requests/{requestId}/assignment", new { assignedToUserId = (int?)null }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("assignedTo").ValueKind);
    }

    [Fact]
    public async Task SetAssignment_AdminAssignsToSupportAgent_ReturnsOk()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_adminClient, categoryId);
        var agentClient = await CreateAuthenticatedClientAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);
        var agentId = await GetUserIdAsync(agentClient);

        var response = await _adminClient.PatchAsJsonAsync(
            $"/api/requests/{requestId}/assignment", new { assignedToUserId = agentId }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SetAssignment_AdminAssignsToEmployee_ReturnsConflict()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_adminClient, categoryId);
        var employeeClient = await CreateAuthenticatedClientAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);
        var employeeId = await GetUserIdAsync(employeeClient);

        var response = await _adminClient.PatchAsJsonAsync(
            $"/api/requests/{requestId}/assignment", new { assignedToUserId = employeeId }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task SetAssignment_WithMissingAssignee_ReturnsNotFound()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_adminClient, categoryId);

        var response = await _adminClient.PatchAsJsonAsync(
            $"/api/requests/{requestId}/assignment", new { assignedToUserId = 999999 }, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task SetAssignment_WithMissingRequest_ReturnsNotFound()
    {
        var agentClient = await CreateAuthenticatedClientAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);
        var agentId = await GetUserIdAsync(agentClient);

        var response = await _adminClient.PatchAsJsonAsync(
            "/api/requests/999999/assignment", new { assignedToUserId = agentId }, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SetAssignment_WithNegativeAssigneeId_ReturnsBadRequest()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_adminClient, categoryId);

        var response = await _adminClient.PatchAsJsonAsync(
            $"/api/requests/{requestId}/assignment", new { assignedToUserId = -1 }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetAssignment_WithInvalidRequestId_ReturnsBadRequest()
    {
        var response = await _adminClient.PatchAsJsonAsync(
            "/api/requests/0/assignment", new { assignedToUserId = (int?)null }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetAssignment_IdempotentSameAssignment_ReturnsOk()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_adminClient, categoryId);
        var agentClient = await CreateAuthenticatedClientAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);
        var agentId = await GetUserIdAsync(agentClient);
        await _adminClient.PatchAsJsonAsync($"/api/requests/{requestId}/assignment", new { assignedToUserId = agentId }, JsonOptions);

        var response = await _adminClient.PatchAsJsonAsync(
            $"/api/requests/{requestId}/assignment", new { assignedToUserId = agentId }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
