using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceRequest.IntegrationTests.TestSupport;

namespace ServiceRequest.IntegrationTests.Requests;

public sealed class RequestsCreationTests : IAsyncLifetime
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

    private async Task DeactivateCategoryAsync(int categoryId)
    {
        var response = await _adminClient.PatchAsJsonAsync(
            $"/api/categories/{categoryId}/active-state",
            new { isActive = false },
            JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string username, string password)
    {
        var client = _factory.CreateClient();
        await client.AuthenticateAsync(username, password);
        return client;
    }

    [Fact]
    public async Task Create_WhenAnonymous_ReturnsUnauthorized()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/requests",
            new { title = "Printer not working", description = "The office printer jams repeatedly.", categoryId, priority = "Medium" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WhenEmployee_ReturnsCreatedWithCorrectCreatorStatusAndAssignee()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var employeeClient = await CreateAuthenticatedClientAsync(ApiTestFactory.EmployeeUsername, ApiTestFactory.EmployeePassword);

        var response = await employeeClient.PostAsJsonAsync(
            "/api/requests",
            new { title = "Laptop does not start", description = "The power button does not respond at all.", categoryId, priority = "High" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/api/requests/", response.Headers.Location!.ToString());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("New", body.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("assignedTo").ValueKind);

        var meResponse = await employeeClient.GetAsync("/api/auth/me");
        var me = await meResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(me.GetProperty("id").GetInt32(), body.GetProperty("createdBy").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Create_WhenSupportAgent_CreatesRequestForThemselves()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        var agentClient = await CreateAuthenticatedClientAsync(ApiTestFactory.SupportAgentUsername, ApiTestFactory.SupportAgentPassword);

        var response = await agentClient.PostAsJsonAsync(
            "/api/requests",
            new { title = "Laptop does not start", description = "The power button does not respond at all.", categoryId, priority = "High" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var meResponse = await agentClient.GetAsync("/api/auth/me");
        var me = await meResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(me.GetProperty("id").GetInt32(), body.GetProperty("createdBy").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Create_WhenAdmin_CreatesRequestForThemselves()
    {
        var categoryId = await CreateCategoryAsync("Hardware");

        var response = await _adminClient.PostAsJsonAsync(
            "/api/requests",
            new { title = "Laptop does not start", description = "The power button does not respond at all.", categoryId, priority = "High" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var meResponse = await _adminClient.GetAsync("/api/auth/me");
        var me = await meResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(me.GetProperty("id").GetInt32(), body.GetProperty("createdBy").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Create_NormalizesWhitespaceInTitleAndDescription()
    {
        var categoryId = await CreateCategoryAsync("Hardware");

        var response = await _adminClient.PostAsJsonAsync(
            "/api/requests",
            new
            {
                title = "  Laptop does not start  ",
                description = "  The power button does not respond at all.  ",
                categoryId,
                priority = "High",
            },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Laptop does not start", body.GetProperty("title").GetString());

        var detailsResponse = await _adminClient.GetAsync(response.Headers.Location);
        var details = await detailsResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("The power button does not respond at all.", details.GetProperty("description").GetString());
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("   ")]
    public async Task Create_WithInvalidTitle_ReturnsBadRequest(string title)
    {
        var categoryId = await CreateCategoryAsync("Hardware");

        var response = await _adminClient.PostAsJsonAsync(
            "/api/requests",
            new { title, description = "A description long enough to pass validation.", categoryId, priority = "Medium" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("too short")]
    [InlineData("   ")]
    public async Task Create_WithInvalidDescription_ReturnsBadRequest(string description)
    {
        var categoryId = await CreateCategoryAsync("Hardware");

        var response = await _adminClient.PostAsJsonAsync(
            "/api/requests",
            new { title = "A valid title", description, categoryId, priority = "Medium" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithInvalidPriority_ReturnsBadRequest()
    {
        var categoryId = await CreateCategoryAsync("Hardware");

        var response = await _adminClient.PostAsJsonAsync(
            "/api/requests",
            new { title = "A valid title", description = "A description long enough to pass validation.", categoryId, priority = "NotARealPriority" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithMissingCategory_ReturnsNotFound()
    {
        var response = await _adminClient.PostAsJsonAsync(
            "/api/requests",
            new { title = "A valid title", description = "A description long enough to pass validation.", categoryId = 999999, priority = "Medium" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Create_WithInactiveCategory_ReturnsConflict()
    {
        var categoryId = await CreateCategoryAsync("Hardware");
        await DeactivateCategoryAsync(categoryId);

        var response = await _adminClient.PostAsJsonAsync(
            "/api/requests",
            new { title = "A valid title", description = "A description long enough to pass validation.", categoryId, priority = "Medium" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
