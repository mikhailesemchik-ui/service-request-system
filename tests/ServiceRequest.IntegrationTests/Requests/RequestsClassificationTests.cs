using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceRequest.IntegrationTests.TestSupport;

namespace ServiceRequest.IntegrationTests.Requests;

public sealed class RequestsClassificationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = HttpClientAuthenticationExtensions.JsonOptions;

    private readonly ApiTestFactory _factory = new();
    private HttpClient _adminClient = null!;
    private HttpClient _agentClient = null!;
    private HttpClient _employeeClient = null!;

    public async Task InitializeAsync()
    {
        _adminClient = _factory.CreateClient();
        await _adminClient.AuthenticateAsync(ApiTestFactory.AdminUsername, ApiTestFactory.AdminPassword);

        _agentClient = _factory.CreateClient();
        await _agentClient.AuthenticateAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);

        _employeeClient = _factory.CreateClient();
        await _employeeClient.AuthenticateAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);
    }

    public Task DisposeAsync()
    {
        _adminClient.Dispose();
        _agentClient.Dispose();
        _employeeClient.Dispose();
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

    private async Task<int> CreateRequestAsync(HttpClient client, int categoryId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/requests",
            new { title = "Printer not working", description = "The office printer jams every time it is used.", categoryId, priority = "Low" },
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetInt32();
    }

    private Task<HttpResponseMessage> PatchClassificationAsync(
        HttpClient client, int requestId, int categoryId, string priority) =>
        client.PatchAsJsonAsync(
            $"/api/requests/{requestId}/classification",
            new { categoryId, priority },
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
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetInt32();
    }

    // Authentication and authorization

    [Fact]
    public async Task PatchClassification_WhenAnonymous_ReturnsUnauthorized()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_adminClient, categoryId);
        var anonymousClient = _factory.CreateClient();

        var response = await PatchClassificationAsync(anonymousClient, requestId, categoryId, "Medium");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PatchClassification_WhenEmployee_ReturnsForbidden()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);

        var response = await PatchClassificationAsync(_employeeClient, requestId, categoryId, "Medium");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PatchClassification_WhenAgentNotAssigned_ReturnsForbidden()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);

        var response = await PatchClassificationAsync(_agentClient, requestId, categoryId, "Medium");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PatchClassification_WhenAgentAssigned_ReturnsOk()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var newCategoryId = await CreateCategoryAsync("Software");
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);
        await AssignRequestAsync(requestId, _agentClient);

        var response = await PatchClassificationAsync(_agentClient, requestId, newCategoryId, "High");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PatchClassification_WhenAdmin_ReturnsOk()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var newCategoryId = await CreateCategoryAsync("Software");
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);

        var response = await PatchClassificationAsync(_adminClient, requestId, newCategoryId, "High");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Terminal status

    [Fact]
    public async Task PatchClassification_WhenClosed_ReturnsConflict()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);
        await AssignRequestAsync(requestId, _agentClient);
        await _agentClient.PatchAsJsonAsync($"/api/requests/{requestId}/status", new { status = "InProgress" }, JsonOptions);
        await _agentClient.PatchAsJsonAsync($"/api/requests/{requestId}/status", new { status = "Resolved" }, JsonOptions);
        await _employeeClient.PatchAsJsonAsync($"/api/requests/{requestId}/status", new { status = "Closed" }, JsonOptions);

        var response = await PatchClassificationAsync(_adminClient, requestId, categoryId, "Medium");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PatchClassification_WhenCancelled_ReturnsConflict()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);
        await _employeeClient.PatchAsJsonAsync($"/api/requests/{requestId}/status", new { status = "Cancelled" }, JsonOptions);

        var response = await PatchClassificationAsync(_adminClient, requestId, categoryId, "Medium");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // Category validation

    [Fact]
    public async Task PatchClassification_WhenCategoryMissing_ReturnsNotFound()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);

        var response = await PatchClassificationAsync(_adminClient, requestId, 99999, "Medium");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchClassification_WhenCategoryInactive_ReturnsConflict()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var inactiveCategoryId = await CreateCategoryAsync("Archived");
        await _adminClient.PatchAsJsonAsync($"/api/categories/{inactiveCategoryId}/active-state", new { isActive = false }, JsonOptions);
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);

        var response = await PatchClassificationAsync(_adminClient, requestId, inactiveCategoryId, "Medium");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // Request body validation

    [Fact]
    public async Task PatchClassification_WhenCategoryIdIsZero_ReturnsBadRequest()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);

        var response = await PatchClassificationAsync(_adminClient, requestId, 0, "Medium");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchClassification_WhenRequestIdIsZero_ReturnsBadRequest()
    {
        var categoryId = await CreateCategoryAsync("Hardware");

        var response = await PatchClassificationAsync(_adminClient, 0, categoryId, "Medium");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Response shape

    [Fact]
    public async Task PatchClassification_WhenSuccessful_ReturnsUpdatedDetails()
    {
        var oldCategoryId = await CreateCategoryAsync("Hardware");
        var newCategoryId = await CreateCategoryAsync("Software");
        var requestId = await CreateRequestAsync(_employeeClient, oldCategoryId);

        var response = await PatchClassificationAsync(_adminClient, requestId, newCategoryId, "High");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(newCategoryId, body.GetProperty("category").GetProperty("id").GetInt32());
        Assert.Equal("High", body.GetProperty("priority").GetString());
    }

    [Fact]
    public async Task PatchClassification_WhenNothingChanges_ReturnsOkWithNoHistory()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var requestId = await CreateRequestAsync(_employeeClient, categoryId);

        var response = await PatchClassificationAsync(_adminClient, requestId, categoryId, "Low");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var historyResponse = await _adminClient.GetAsync($"/api/requests/{requestId}/history");
        var history = await historyResponse.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOptions);
        Assert.Empty(history!);
    }
}
