using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceRequest.IntegrationTests.TestSupport;

namespace ServiceRequest.IntegrationTests.Requests;

public sealed class RequestsListTests : IAsyncLifetime
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
            "/api/categories",
            new { name, description = (string?)null },
            JsonOptions);
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

    private static async Task<int> CreateRequestAsync(
        HttpClient client, int categoryId, string title = "Printer not working", string priority = "Medium")
    {
        var response = await client.PostAsJsonAsync(
            "/api/requests",
            new { title, description = "The office printer jams every time it is used.", categoryId, priority },
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task GetList_WhenAnonymous_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/requests");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetList_WhenEmployee_ReturnsOnlyOwnRequests()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var employeeClient = await CreateAuthenticatedClientAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);
        await CreateRequestAsync(employeeClient, categoryId, "Employee's own request");
        await CreateRequestAsync(_adminClient, categoryId, "Admin's own request");

        var response = await employeeClient.GetAsync("/api/requests");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var items = body.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("Employee's own request", items[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task GetList_WhenSupportAgent_ReturnsAllRequests()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var employeeClient = await CreateAuthenticatedClientAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);
        var agentClient = await CreateAuthenticatedClientAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);
        await CreateRequestAsync(employeeClient, categoryId);
        await CreateRequestAsync(_adminClient, categoryId);

        var response = await agentClient.GetAsync("/api/requests");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
        Assert.Equal(2, body.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task GetList_WhenAdmin_ReturnsAllRequests()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var employeeClient = await CreateAuthenticatedClientAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);
        await CreateRequestAsync(employeeClient, categoryId);
        await CreateRequestAsync(_adminClient, categoryId);

        var response = await _adminClient.GetAsync("/api/requests");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task GetList_WhenDatabaseIsEmpty_ReturnsEmptyPagedResponse()
    {
        var response = await _adminClient.GetAsync("/api/requests");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Empty(body.GetProperty("items").EnumerateArray());
        Assert.Equal(0, body.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task GetList_WithDefaultPagination_UsesPage1AndPageSize20()
    {
        var response = await _adminClient.GetAsync("/api/requests");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(20, body.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task GetList_WithCustomPageAndPageSize_ReturnsRequestedSlice()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        for (var i = 0; i < 5; i++)
        {
            await CreateRequestAsync(_adminClient, categoryId, $"Request {i}");
        }

        var response = await _adminClient.GetAsync("/api/requests?page=2&pageSize=2");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
        Assert.Equal(2, body.GetProperty("page").GetInt32());
        Assert.Equal(2, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(5, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, body.GetProperty("totalPages").GetInt32());
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    public async Task GetList_WithInvalidPage_ReturnsBadRequest(string queryString)
    {
        var response = await _adminClient.GetAsync($"/api/requests?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task GetList_WithInvalidPageSize_ReturnsBadRequest(string queryString)
    {
        var response = await _adminClient.GetAsync($"/api/requests?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetList_WithStatusFilter_ReturnsOnlyMatchingRequests()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        await CreateRequestAsync(_adminClient, categoryId);

        var matching = await _adminClient.GetAsync("/api/requests?status=New");
        var nonMatching = await _adminClient.GetAsync("/api/requests?status=Resolved");

        var matchingBody = await matching.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var nonMatchingBody = await nonMatching.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(1, matchingBody.GetProperty("items").GetArrayLength());
        Assert.Empty(nonMatchingBody.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task GetList_WithPriorityFilter_ReturnsOnlyMatchingRequests()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        await CreateRequestAsync(_adminClient, categoryId, "High priority request", "High");
        await CreateRequestAsync(_adminClient, categoryId, "Low priority request", "Low");

        var response = await _adminClient.GetAsync("/api/requests?priority=High");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var items = body.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("High priority request", items[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task GetList_WithCategoryFilter_ReturnsOnlyMatchingRequests()
    {
        var hardwareId = await CreateCategoryAsync("Hardware");
        var softwareId = await CreateCategoryAsync("Software");
        await CreateRequestAsync(_adminClient, hardwareId, "Hardware issue");
        await CreateRequestAsync(_adminClient, softwareId, "Software issue");

        var response = await _adminClient.GetAsync($"/api/requests?categoryId={hardwareId}");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var items = body.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("Hardware issue", items[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task GetList_WithInvalidEnumFilter_ReturnsBadRequest()
    {
        var response = await _adminClient.GetAsync("/api/requests?status=NotARealStatus");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetList_ReturnsResultsOrderedNewestFirst()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var firstId = await CreateRequestAsync(_adminClient, categoryId, "First");
        var secondId = await CreateRequestAsync(_adminClient, categoryId, "Second");
        var thirdId = await CreateRequestAsync(_adminClient, categoryId, "Third");

        var response = await _adminClient.GetAsync("/api/requests");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var ids = body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt32()).ToList();
        Assert.Equal([thirdId, secondId, firstId], ids);
    }
}
