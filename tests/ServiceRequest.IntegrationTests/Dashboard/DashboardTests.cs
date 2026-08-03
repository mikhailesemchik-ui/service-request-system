using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceRequest.IntegrationTests.TestSupport;

namespace ServiceRequest.IntegrationTests.Dashboard;

public sealed class DashboardTests : IAsyncLifetime
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
            "/api/categories",
            new { name, description = (string?)null },
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetInt32();
    }

    private async Task<int> CreateRequestAsync(HttpClient client, int categoryId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/requests",
            new { title = "Test request", description = "Test description for dashboard.", categoryId, priority = "Medium" },
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task GetSummary_WhenAnonymous_ReturnsUnauthorized()
    {
        var anonymousClient = _factory.CreateClient();

        var response = await anonymousClient.GetAsync("/api/dashboard/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_WhenEmployee_ReturnsOk()
    {
        var response = await _employeeClient.GetAsync("/api/dashboard/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_WhenEmployee_ScopeIsEmployee()
    {
        var response = await _employeeClient.GetAsync("/api/dashboard/summary");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal("Employee", body.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task GetSummary_WhenEmployee_ResponseIsScopedToOwnRequests()
    {
        var categoryId = await CreateCategoryAsync("Employee Dashboard Test");

        // Employee creates 1 request; admin creates 1 request
        await CreateRequestAsync(_employeeClient, categoryId);
        await CreateRequestAsync(_adminClient, categoryId);

        var response = await _employeeClient.GetAsync("/api/dashboard/summary");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(1, body.GetProperty("totalRequests").GetInt32());
    }

    [Fact]
    public async Task GetSummary_WhenSupportAgent_ReturnsOperationalMetrics()
    {
        var response = await _agentClient.GetAsync("/api/dashboard/summary");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal("SupportAgent", body.GetProperty("scope").GetString());
        Assert.NotNull(body.GetProperty("staffMetrics").GetRawText());
        Assert.Equal("null", body.GetProperty("adminMetrics").GetRawText());
    }

    [Fact]
    public async Task GetSummary_WhenAdmin_ReturnsAdditionalAdminMetrics()
    {
        var response = await _adminClient.GetAsync("/api/dashboard/summary");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal("Admin", body.GetProperty("scope").GetString());
        Assert.NotNull(body.GetProperty("staffMetrics").GetRawText());
        var adminMetrics = body.GetProperty("adminMetrics");
        Assert.NotEqual(JsonValueKind.Null, adminMetrics.ValueKind);
        Assert.True(adminMetrics.TryGetProperty("activeCategories", out _));
        Assert.True(adminMetrics.TryGetProperty("activeSupportAgents", out _));
        Assert.True(adminMetrics.TryGetProperty("activeAdmins", out _));
    }

    [Fact]
    public async Task GetSummary_WhenEmployee_StaffAndAdminMetricsAreNull()
    {
        var response = await _employeeClient.GetAsync("/api/dashboard/summary");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal("null", body.GetProperty("staffMetrics").GetRawText());
        Assert.Equal("null", body.GetProperty("adminMetrics").GetRawText());
    }

    [Fact]
    public async Task GetSummary_ResponseContainsAllSixStatuses()
    {
        var response = await _employeeClient.GetAsync("/api/dashboard/summary");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var statusCounts = body.GetProperty("statusCounts").EnumerateArray().ToList();
        Assert.Equal(6, statusCounts.Count);
        var statuses = statusCounts.Select(s => s.GetProperty("status").GetString()).ToList();
        Assert.Contains("New", statuses);
        Assert.Contains("InProgress", statuses);
        Assert.Contains("WaitingForUser", statuses);
        Assert.Contains("Resolved", statuses);
        Assert.Contains("Closed", statuses);
        Assert.Contains("Cancelled", statuses);
    }

    [Fact]
    public async Task GetSummary_ResponseContainsAllFourPriorities()
    {
        var response = await _employeeClient.GetAsync("/api/dashboard/summary");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var priorityCounts = body.GetProperty("priorityCounts").EnumerateArray().ToList();
        Assert.Equal(4, priorityCounts.Count);
        var priorities = priorityCounts.Select(p => p.GetProperty("priority").GetString()).ToList();
        Assert.Contains("Low", priorities);
        Assert.Contains("Medium", priorities);
        Assert.Contains("High", priorities);
        Assert.Contains("Critical", priorities);
    }

    [Fact]
    public async Task GetSummary_RecentRequestsContainAtMostFiveItems()
    {
        var categoryId = await CreateCategoryAsync("Recent Limit Test");

        for (var i = 0; i < 7; i++)
        {
            await CreateRequestAsync(_employeeClient, categoryId);
        }

        var response = await _employeeClient.GetAsync("/api/dashboard/summary");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var recentRequests = body.GetProperty("recentRequests").EnumerateArray().ToList();
        Assert.True(recentRequests.Count <= 5);
    }

    [Fact]
    public async Task GetSummary_ResponseShapeIsCorrect()
    {
        var response = await _employeeClient.GetAsync("/api/dashboard/summary");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.True(body.TryGetProperty("scope", out _));
        Assert.True(body.TryGetProperty("totalRequests", out _));
        Assert.True(body.TryGetProperty("openRequests", out _));
        Assert.True(body.TryGetProperty("resolvedRequests", out _));
        Assert.True(body.TryGetProperty("closedRequests", out _));
        Assert.True(body.TryGetProperty("cancelledRequests", out _));
        Assert.True(body.TryGetProperty("statusCounts", out _));
        Assert.True(body.TryGetProperty("priorityCounts", out _));
        Assert.True(body.TryGetProperty("recentRequests", out _));
    }

    [Fact]
    public async Task GetSummary_WhenSupportAgent_RecentRequestsIncludeAllUsersRequests()
    {
        var categoryId = await CreateCategoryAsync("Agent Recents Test");
        await CreateRequestAsync(_employeeClient, categoryId);
        await CreateRequestAsync(_adminClient, categoryId);

        var response = await _agentClient.GetAsync("/api/dashboard/summary");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var recentCount = body.GetProperty("recentRequests").GetArrayLength();
        Assert.True(recentCount >= 2);
    }
}
