using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceRequest.IntegrationTests.TestSupport;

namespace ServiceRequest.IntegrationTests.Requests;

public sealed class RequestsCommentsTests : IAsyncLifetime
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

    private static Task<HttpResponseMessage> PostCommentAsync(
        HttpClient client, int requestId, string content, bool isInternal = false) =>
        client.PostAsJsonAsync(
            $"/api/requests/{requestId}/comments",
            new { content, isInternal },
            JsonOptions);

    // GET /api/requests/{requestId}/comments

    [Fact]
    public async Task GetComments_WhenAnonymous_ReturnsUnauthorized()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_adminClient, categoryId);
        var anonymousClient = _factory.CreateClient();

        var response = await anonymousClient.GetAsync($"/api/requests/{requestId}/comments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetComments_WhenEmpty_ReturnsOkWithEmptyArray()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_adminClient, categoryId);

        var response = await _adminClient.GetAsync($"/api/requests/{requestId}/comments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOptions);
        Assert.NotNull(body);
        Assert.Empty(body!);
    }

    [Fact]
    public async Task GetComments_WhenEmployeeRequestsOwnRequest_ReturnsPublicCommentsOnly()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var employeeClient = await CreateAuthenticatedClientAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);
        var requestId = await CreateRequestAsync(employeeClient, categoryId);
        await PostCommentAsync(_adminClient, requestId, "Staff public comment", false);
        await PostCommentAsync(_adminClient, requestId, "Staff internal note", true);

        var response = await employeeClient.GetAsync($"/api/requests/{requestId}/comments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var comments = await response.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOptions);
        Assert.NotNull(comments);
        Assert.Single(comments!);
        Assert.Equal("Staff public comment", comments![0].GetProperty("content").GetString());
        Assert.False(comments[0].GetProperty("isInternal").GetBoolean());
    }

    [Fact]
    public async Task GetComments_WhenEmployeeRequestsAnotherUsersRequest_ReturnsNotFound()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_adminClient, categoryId);
        var employeeClient = await CreateAuthenticatedClientAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);

        var response = await employeeClient.GetAsync($"/api/requests/{requestId}/comments");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetComments_WhenStaff_ReturnsBothPublicAndInternalComments()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_adminClient, categoryId);
        await PostCommentAsync(_adminClient, requestId, "Public comment", false);
        await PostCommentAsync(_adminClient, requestId, "Internal note", true);

        var agentClient = await CreateAuthenticatedClientAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);
        var response = await agentClient.GetAsync($"/api/requests/{requestId}/comments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var comments = await response.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOptions);
        Assert.NotNull(comments);
        Assert.Equal(2, comments!.Count);
    }

    [Fact]
    public async Task GetComments_InternalCommentIncludesCorrectFields()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_adminClient, categoryId);
        await PostCommentAsync(_adminClient, requestId, "Staff note", true);

        var response = await _adminClient.GetAsync($"/api/requests/{requestId}/comments");

        var comments = await response.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOptions);
        Assert.NotNull(comments);
        var comment = Assert.Single(comments!);
        Assert.True(comment.GetProperty("isInternal").GetBoolean());
        Assert.Equal("Staff note", comment.GetProperty("content").GetString());
        Assert.True(comment.GetProperty("author").TryGetProperty("displayName", out _));
        Assert.True(comment.TryGetProperty("createdAt", out _));
    }

    [Fact]
    public async Task GetComments_WithInvalidRequestId_ReturnsBadRequest()
    {
        var response = await _adminClient.GetAsync("/api/requests/0/comments");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetComments_WithMissingRequest_ReturnsNotFound()
    {
        var response = await _adminClient.GetAsync("/api/requests/999999/comments");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // POST /api/requests/{requestId}/comments

    [Fact]
    public async Task AddComment_WhenAnonymous_ReturnsUnauthorized()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_adminClient, categoryId);
        var anonymousClient = _factory.CreateClient();

        var response = await PostCommentAsync(anonymousClient, requestId, "Hello");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddComment_WhenEmployeeAddsPublicComment_ReturnsCreated()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var employeeClient = await CreateAuthenticatedClientAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);
        var requestId = await CreateRequestAsync(employeeClient, categoryId);

        var response = await PostCommentAsync(employeeClient, requestId, "I'm still having the issue.", false);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("I'm still having the issue.", body.GetProperty("content").GetString());
        Assert.False(body.GetProperty("isInternal").GetBoolean());
    }

    [Fact]
    public async Task AddComment_WhenEmployeeAddsToAnotherUsersRequest_ReturnsNotFound()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_adminClient, categoryId);
        var employeeClient = await CreateAuthenticatedClientAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);

        var response = await PostCommentAsync(employeeClient, requestId, "Should not work");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AddComment_WhenEmployeeAddsInternalComment_ReturnsForbidden()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var employeeClient = await CreateAuthenticatedClientAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);
        var requestId = await CreateRequestAsync(employeeClient, categoryId);

        var response = await PostCommentAsync(employeeClient, requestId, "Secret note", true);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AddComment_WhenStaffAddsInternalComment_ReturnsCreated()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_adminClient, categoryId);
        var agentClient = await CreateAuthenticatedClientAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);

        var response = await PostCommentAsync(agentClient, requestId, "Internal investigation note.", true);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(body.GetProperty("isInternal").GetBoolean());
    }

    [Fact]
    public async Task AddComment_ToClosedRequest_ReturnsConflict()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var agentClient = await CreateAuthenticatedClientAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);
        var requestId = await CreateRequestAsync(_adminClient, categoryId);

        var agentId = await GetUserIdAsync(agentClient);
        await _adminClient.PatchAsJsonAsync($"/api/requests/{requestId}/assignment", new { assignedToUserId = agentId }, JsonOptions);
        await agentClient.PatchAsJsonAsync($"/api/requests/{requestId}/status", new { status = "InProgress" }, JsonOptions);
        await agentClient.PatchAsJsonAsync($"/api/requests/{requestId}/status", new { status = "Resolved" }, JsonOptions);
        await _adminClient.PatchAsJsonAsync($"/api/requests/{requestId}/status", new { status = "Closed" }, JsonOptions);

        var response = await PostCommentAsync(_adminClient, requestId, "Too late.");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AddComment_ToCancelledRequest_ReturnsConflict()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_adminClient, categoryId);
        await _adminClient.PatchAsJsonAsync($"/api/requests/{requestId}/status", new { status = "Cancelled" }, JsonOptions);

        var response = await PostCommentAsync(_adminClient, requestId, "Too late.");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AddComment_WithEmptyContent_ReturnsBadRequest()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_adminClient, categoryId);

        var response = await PostCommentAsync(_adminClient, requestId, "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddComment_WithInvalidRequestId_ReturnsBadRequest()
    {
        var response = await PostCommentAsync(_adminClient, 0, "Hello");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddComment_WithMissingRequest_ReturnsNotFound()
    {
        var response = await PostCommentAsync(_adminClient, 999999, "Hello");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<int> GetUserIdAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/me");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetInt32();
    }
}
