using System.Net;
using System.Net.Http.Json;
using ServiceRequest.Application.Categories;
using ServiceRequest.IntegrationTests.TestSupport;

namespace ServiceRequest.IntegrationTests.Categories;

public sealed class CategoriesControllerTests : IAsyncLifetime
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = HttpClientAuthenticationExtensions.JsonOptions;

    private readonly ApiTestFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        await _client.AuthenticateAsync(ApiTestFactory.AdminUsername, ApiTestFactory.AdminPassword);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private async Task<CategoryDto> CreateCategoryAsync(string name, string? description = null)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/categories",
            new { name, description },
            JsonOptions);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);
        Assert.NotNull(created);
        return created!;
    }

    // Listing

    [Fact]
    public async Task GetAll_WhenDatabaseIsEmpty_ReturnsOkAndEmptyArray()
    {
        var response = await _client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>(JsonOptions);
        Assert.NotNull(categories);
        Assert.Empty(categories!);
    }

    [Fact]
    public async Task GetAll_ByDefault_ReturnsOnlyActiveCategories()
    {
        var active = await CreateCategoryAsync("Hardware");
        var toDeactivate = await CreateCategoryAsync("Software");
        await _client.PatchAsJsonAsync(
            $"/api/categories/{toDeactivate.Id}/active-state",
            new { isActive = false },
            JsonOptions);

        var response = await _client.GetAsync("/api/categories");

        var categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>(JsonOptions);
        Assert.NotNull(categories);
        Assert.Single(categories!);
        Assert.Equal(active.Id, categories![0].Id);
    }

    [Fact]
    public async Task GetAll_WhenIncludeInactiveIsTrue_ReturnsActiveAndInactiveCategories()
    {
        await CreateCategoryAsync("Hardware");
        var toDeactivate = await CreateCategoryAsync("Software");
        await _client.PatchAsJsonAsync(
            $"/api/categories/{toDeactivate.Id}/active-state",
            new { isActive = false },
            JsonOptions);

        var response = await _client.GetAsync("/api/categories?includeInactive=true");

        var categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>(JsonOptions);
        Assert.NotNull(categories);
        Assert.Equal(2, categories!.Count);
    }

    [Fact]
    public async Task GetAll_ReturnsCategoriesOrderedByName()
    {
        await CreateCategoryAsync("Zebra");
        await CreateCategoryAsync("Apple");
        await CreateCategoryAsync("Mango");

        var response = await _client.GetAsync("/api/categories");

        var categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>(JsonOptions);
        Assert.NotNull(categories);
        Assert.Equal(["Apple", "Mango", "Zebra"], categories!.Select(c => c.Name));
    }

    // Retrieval

    [Fact]
    public async Task GetById_WhenCategoryExists_ReturnsOk()
    {
        var created = await CreateCategoryAsync("Hardware", "Computer issues");

        var response = await _client.GetAsync($"/api/categories/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var category = await response.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);
        Assert.NotNull(category);
        Assert.Equal("Hardware", category!.Name);
        Assert.Equal("Computer issues", category.Description);
    }

    [Fact]
    public async Task GetById_WhenCategoryDoesNotExist_ReturnsNotFoundProblemDetails()
    {
        var response = await _client.GetAsync("/api/categories/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetById_WhenCategoryIsInactive_StillReturnsOk()
    {
        var created = await CreateCategoryAsync("Hardware");
        await _client.PatchAsJsonAsync(
            $"/api/categories/{created.Id}/active-state",
            new { isActive = false },
            JsonOptions);

        var response = await _client.GetAsync($"/api/categories/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var category = await response.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);
        Assert.NotNull(category);
        Assert.False(category!.IsActive);
    }

    // Creation

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreatedWithLocationAndNormalizedValues()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/categories",
            new { name = "  Hardware  ", description = "  Computer issues  " },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/api/categories/", response.Headers.Location!.ToString());

        var category = await response.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);
        Assert.NotNull(category);
        Assert.Equal("Hardware", category!.Name);
        Assert.Equal("Computer issues", category.Description);
        Assert.True(category.IsActive);
    }

    [Fact]
    public async Task Create_WithValidRequest_PersistsCategory()
    {
        var created = await CreateCategoryAsync("Hardware", "Computer issues");

        var getResponse = await _client.GetAsync($"/api/categories/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task Create_WithWhitespaceOnlyName_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/categories",
            new { name = "   ", description = (string?)null },
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithTooLongDescription_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/categories",
            new { name = "Hardware", description = new string('a', 501) },
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithDuplicateName_ReturnsConflict()
    {
        await CreateCategoryAsync("Hardware");

        var response = await _client.PostAsJsonAsync(
            "/api/categories",
            new { name = "Hardware", description = (string?)null },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Create_WithDuplicateNameDifferentCasing_ReturnsConflict()
    {
        await CreateCategoryAsync("Hardware");

        var response = await _client.PostAsJsonAsync(
            "/api/categories",
            new { name = "hardware", description = (string?)null },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithDuplicateNameSurroundingWhitespace_ReturnsConflict()
    {
        await CreateCategoryAsync("Hardware");

        var response = await _client.PostAsJsonAsync(
            "/api/categories",
            new { name = " HARDWARE ", description = (string?)null },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // Updating

    [Fact]
    public async Task Update_WithValidRequest_ReturnsOkAndPersistsChanges()
    {
        var created = await CreateCategoryAsync("Hardware", "Original description");

        var response = await _client.PutAsJsonAsync(
            $"/api/categories/{created.Id}",
            new { name = "Hardware Support", description = "Updated description" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Hardware Support", updated!.Name);
        Assert.Equal("Updated description", updated.Description);

        var getResponse = await _client.GetAsync($"/api/categories/{created.Id}");
        var persisted = await getResponse.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);
        Assert.Equal("Hardware Support", persisted!.Name);
    }

    [Fact]
    public async Task Update_WhenCategoryDoesNotExist_ReturnsNotFound()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/categories/999999",
            new { name = "Hardware", description = (string?)null },
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Update_WithDuplicateName_ReturnsConflict()
    {
        await CreateCategoryAsync("Hardware");
        var toUpdate = await CreateCategoryAsync("Software");

        var response = await _client.PutAsJsonAsync(
            $"/api/categories/{toUpdate.Id}",
            new { name = "Hardware", description = (string?)null },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithoutChangingEffectiveName_Succeeds()
    {
        var created = await CreateCategoryAsync("Hardware", "Original description");

        var response = await _client.PutAsJsonAsync(
            $"/api/categories/{created.Id}",
            new { name = "Hardware", description = "Updated description" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_DoesNotChangeIsActive()
    {
        var created = await CreateCategoryAsync("Hardware");
        await _client.PatchAsJsonAsync(
            $"/api/categories/{created.Id}/active-state",
            new { isActive = false },
            JsonOptions);

        var response = await _client.PutAsJsonAsync(
            $"/api/categories/{created.Id}",
            new { name = "Hardware Support", description = (string?)null },
            JsonOptions);

        var updated = await response.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);
        Assert.NotNull(updated);
        Assert.False(updated!.IsActive);
    }

    // Active state

    [Fact]
    public async Task SetActiveState_CanDeactivateCategory()
    {
        var created = await CreateCategoryAsync("Hardware");

        var response = await _client.PatchAsJsonAsync(
            $"/api/categories/{created.Id}/active-state",
            new { isActive = false },
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);
        Assert.NotNull(updated);
        Assert.False(updated!.IsActive);
    }

    [Fact]
    public async Task SetActiveState_CanReactivateCategory()
    {
        var created = await CreateCategoryAsync("Hardware");
        await _client.PatchAsJsonAsync(
            $"/api/categories/{created.Id}/active-state",
            new { isActive = false },
            JsonOptions);

        var response = await _client.PatchAsJsonAsync(
            $"/api/categories/{created.Id}/active-state",
            new { isActive = true },
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);
        Assert.NotNull(updated);
        Assert.True(updated!.IsActive);
    }

    [Fact]
    public async Task SetActiveState_WhenRepeatedWithSameState_RemainsSuccessful()
    {
        var created = await CreateCategoryAsync("Hardware");

        var first = await _client.PatchAsJsonAsync(
            $"/api/categories/{created.Id}/active-state",
            new { isActive = false },
            JsonOptions);
        var second = await _client.PatchAsJsonAsync(
            $"/api/categories/{created.Id}/active-state",
            new { isActive = false },
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task SetActiveState_WhenCategoryDoesNotExist_ReturnsNotFound()
    {
        var response = await _client.PatchAsJsonAsync(
            "/api/categories/999999/active-state",
            new { isActive = false },
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
