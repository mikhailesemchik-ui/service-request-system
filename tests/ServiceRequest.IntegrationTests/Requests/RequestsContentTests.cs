using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceRequest.IntegrationTests.TestSupport;

namespace ServiceRequest.IntegrationTests.Requests;

public sealed class RequestsContentTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = HttpClientAuthenticationExtensions.JsonOptions;

    private readonly ApiTestFactory _factory = new();
    private HttpClient _adminClient = null!;
    private HttpClient _agentClient = null!;
    private HttpClient _secondAgentClient = null!;
    private HttpClient _employeeClient = null!;

    public async Task InitializeAsync()
    {
        _adminClient = _factory.CreateClient();
        await _adminClient.AuthenticateAsync(ApiTestFactory.AdminUsername, ApiTestFactory.AdminPassword);

        _agentClient = _factory.CreateClient();
        await _agentClient.AuthenticateAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);

        _secondAgentClient = _factory.CreateClient();
        await _secondAgentClient.AuthenticateAsync(ApiTestFactory.SecondSupportAgentUsername, ApiTestFactory.SecondSupportAgentPassword);

        _employeeClient = _factory.CreateClient();
        await _employeeClient.AuthenticateAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);
    }

    public Task DisposeAsync()
    {
        _adminClient.Dispose();
        _agentClient.Dispose();
        _secondAgentClient.Dispose();
        _employeeClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private async Task<int> CreateCategoryAsync(string name = "Hardware")
    {
        var response = await _adminClient.PostAsJsonAsync(
            "/api/categories",
            new { name, description = (string?)null },
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetInt32();
    }

    private async Task<JsonElement> CreateRequestDetailsAsync(HttpClient client, int categoryId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/requests",
            new
            {
                title = "Printer not working",
                description = "The office printer jams every time it is used.",
                categoryId,
                priority = "Low",
            },
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
    }

    private async Task<int> CreateRequestAsync(HttpClient client, int categoryId)
    {
        var body = await CreateRequestDetailsAsync(client, categoryId);
        return body.GetProperty("id").GetInt32();
    }

    private static Task<HttpResponseMessage> PatchContentAsync(
        HttpClient client,
        int requestId,
        string title = "Updated title",
        string description = "Updated description.") =>
        client.PatchAsJsonAsync(
            $"/api/requests/{requestId}/content",
            new { title, description },
            JsonOptions);

    private async Task AssignRequestAsync(int requestId, HttpClient agentClient)
    {
        var agentId = await GetCurrentUserIdAsync(agentClient);
        var response = await _adminClient.PatchAsJsonAsync(
            $"/api/requests/{requestId}/assignment",
            new { assignedToUserId = agentId },
            JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<int> GetCurrentUserIdAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/me");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetInt32();
    }

    private async Task MoveToInProgressAsync(int requestId)
    {
        await AssignRequestAsync(requestId, _agentClient);
        var response = await _agentClient.PatchAsJsonAsync(
            $"/api/requests/{requestId}/status",
            new { status = "InProgress" },
            JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private async Task MoveToClosedAsync(int requestId)
    {
        await MoveToInProgressAsync(requestId);
        var resolved = await _agentClient.PatchAsJsonAsync(
            $"/api/requests/{requestId}/status",
            new { status = "Resolved" },
            JsonOptions);
        resolved.EnsureSuccessStatusCode();
        var closed = await _employeeClient.PatchAsJsonAsync(
            $"/api/requests/{requestId}/status",
            new { status = "Closed" },
            JsonOptions);
        closed.EnsureSuccessStatusCode();
    }

    private async Task MoveToCancelledAsync(int requestId)
    {
        var response = await _employeeClient.PatchAsJsonAsync(
            $"/api/requests/{requestId}/status",
            new { status = "Cancelled" },
            JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PatchContent_WhenAnonymous_ReturnsUnauthorized()
    {
        var categoryId = await CreateCategoryAsync();
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);
        var anonymousClient = _factory.CreateClient();

        var response = await PatchContentAsync(anonymousClient, requestId);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PatchContent_WhenRequestIdIsZero_ReturnsBadRequest()
    {
        var response = await PatchContentAsync(_adminClient, 0);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchContent_WhenRequestMissing_ReturnsNotFound()
    {
        var response = await PatchContentAsync(_adminClient, 99999);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchContent_WhenEmployeeOwnsNewRequest_ReturnsOk()
    {
        var categoryId = await CreateCategoryAsync();
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);

        var response = await PatchContentAsync(_employeeClient, requestId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PatchContent_WhenEmployeeRequestsAnotherUsersRequest_ReturnsNotFound()
    {
        var categoryId = await CreateCategoryAsync();
        var requestId = await CreateRequestAsync(_adminClient, categoryId);

        var response = await PatchContentAsync(_employeeClient, requestId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchContent_WhenEmployeeOwnsNonNewRequest_ReturnsConflict()
    {
        var categoryId = await CreateCategoryAsync();
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);
        await MoveToInProgressAsync(requestId);

        var response = await PatchContentAsync(_employeeClient, requestId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PatchContent_WhenSupportAgentAssigned_ReturnsOk()
    {
        var categoryId = await CreateCategoryAsync();
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);
        await AssignRequestAsync(requestId, _agentClient);

        var response = await PatchContentAsync(_agentClient, requestId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PatchContent_WhenSupportAgentUnassigned_ReturnsForbidden()
    {
        var categoryId = await CreateCategoryAsync();
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);

        var response = await PatchContentAsync(_agentClient, requestId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PatchContent_WhenRequestAssignedToAnotherSupportAgent_ReturnsForbidden()
    {
        var categoryId = await CreateCategoryAsync();
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);
        await AssignRequestAsync(requestId, _agentClient);

        var response = await PatchContentAsync(_secondAgentClient, requestId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PatchContent_WhenAdminEditsNonTerminalRequest_ReturnsOk()
    {
        var categoryId = await CreateCategoryAsync();
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);

        var response = await PatchContentAsync(_adminClient, requestId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PatchContent_WhenTitleIsBlank_ReturnsBadRequest()
    {
        var categoryId = await CreateCategoryAsync();
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);

        var response = await PatchContentAsync(_employeeClient, requestId, title: "   ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchContent_WhenTitleIsTooShortAfterTrimming_ReturnsBadRequest()
    {
        var categoryId = await CreateCategoryAsync();
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);

        var response = await PatchContentAsync(_employeeClient, requestId, title: "  ab  ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchContent_WhenTitleIsTooLong_ReturnsBadRequest()
    {
        var categoryId = await CreateCategoryAsync();
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);

        var response = await PatchContentAsync(_employeeClient, requestId, title: new string('a', 201));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchContent_WhenDescriptionIsBlank_ReturnsBadRequest()
    {
        var categoryId = await CreateCategoryAsync();
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);

        var response = await PatchContentAsync(_employeeClient, requestId, description: "   ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchContent_WhenDescriptionIsTooLong_ReturnsBadRequest()
    {
        var categoryId = await CreateCategoryAsync();
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);

        var response = await PatchContentAsync(_employeeClient, requestId, description: new string('a', 4001));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchContent_WhenRequestIsClosed_ReturnsConflict()
    {
        var categoryId = await CreateCategoryAsync();
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);
        await MoveToClosedAsync(requestId);

        var response = await PatchContentAsync(_adminClient, requestId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PatchContent_WhenRequestIsCancelled_ReturnsConflict()
    {
        var categoryId = await CreateCategoryAsync();
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);
        await MoveToCancelledAsync(requestId);

        var response = await PatchContentAsync(_adminClient, requestId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PatchContent_WhenSuccessful_ReturnsNormalizedValuesAndUpdatedTimestamp()
    {
        var categoryId = await CreateCategoryAsync();
        var created = await CreateRequestDetailsAsync(_employeeClient, categoryId);
        var requestId = created.GetProperty("id").GetInt32();
        var previousUpdatedAt = DateTimeOffset.Parse(created.GetProperty("updatedAt").GetString()!);

        var response = await PatchContentAsync(
            _employeeClient,
            requestId,
            title: "  Network printer offline  ",
            description: "  Printer disconnects daily.  ");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Network printer offline", body.GetProperty("title").GetString());
        Assert.Equal("Printer disconnects daily.", body.GetProperty("description").GetString());
        Assert.True(DateTimeOffset.Parse(body.GetProperty("updatedAt").GetString()!) > previousUpdatedAt);
    }

    [Fact]
    public async Task PatchContent_WhenSuccessful_HistoryEndpointContainsContentActions()
    {
        var categoryId = await CreateCategoryAsync();
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);

        var response = await PatchContentAsync(_employeeClient, requestId);
        response.EnsureSuccessStatusCode();

        var historyResponse = await _employeeClient.GetAsync($"/api/requests/{requestId}/history");
        historyResponse.EnsureSuccessStatusCode();
        var history = await historyResponse.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOptions);

        Assert.Contains(history!, entry => entry.GetProperty("action").GetString() == "TitleChanged");
        Assert.Contains(history!, entry => entry.GetProperty("action").GetString() == "DescriptionChanged");
    }

    [Fact]
    public async Task PatchContent_WhenRepeatedWithSameNormalizedValues_CreatesNoExtraHistory()
    {
        var categoryId = await CreateCategoryAsync();
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);

        var firstResponse = await PatchContentAsync(_employeeClient, requestId);
        firstResponse.EnsureSuccessStatusCode();
        var secondResponse = await PatchContentAsync(
            _employeeClient,
            requestId,
            title: "  Updated title  ",
            description: "  Updated description.  ");
        secondResponse.EnsureSuccessStatusCode();

        var historyResponse = await _employeeClient.GetAsync($"/api/requests/{requestId}/history");
        var history = await historyResponse.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOptions);

        Assert.Equal(2, history!.Count);
    }
}
