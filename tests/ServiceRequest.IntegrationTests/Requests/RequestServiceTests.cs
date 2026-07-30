using Microsoft.EntityFrameworkCore;
using ServiceRequest.Application.Authentication;
using ServiceRequest.Application.Requests;
using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;
using ServiceRequest.Domain.Exceptions;
using ServiceRequest.Infrastructure.Data;
using ServiceRequest.Infrastructure.Requests;

namespace ServiceRequest.IntegrationTests.Requests;

public sealed class RequestServiceTests : IDisposable
{
    private readonly string _databasePath;
    private readonly string _connectionString;
    private readonly ApplicationDbContext _dbContext;
    private readonly RequestService _sut;

    public RequestServiceTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"service-requests-service-tests-{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_databasePath};Pooling=False";

        _dbContext = CreateContext();
        _dbContext.Database.Migrate();
        _sut = new RequestService(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    private async Task<RequestCategory> CreateCategoryAsync(string name, bool isActive = true)
    {
        var category = new RequestCategory(name);
        _dbContext.RequestCategories.Add(category);
        await _dbContext.SaveChangesAsync();

        if (!isActive)
        {
            category.SetActiveState(false);
            await _dbContext.SaveChangesAsync();
        }

        return category;
    }

    private async Task<ApplicationUser> CreateUserAsync(string username, UserRole role, bool isActive = true)
    {
        var user = new ApplicationUser(username, $"{username} Display", $"{username}@example.test", role);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        if (!isActive)
        {
            user.SetActiveState(false);
            await _dbContext.SaveChangesAsync();
        }

        return user;
    }

    private async Task<SupportRequest> CreateRequestAsync(
        RequestCategory category,
        ApplicationUser creator,
        string title = "Printer not working",
        RequestPriority priority = RequestPriority.Medium)
    {
        var request = new SupportRequest(title, "The office printer jams repeatedly.", priority, category, creator);
        _dbContext.SupportRequests.Add(request);
        await _dbContext.SaveChangesAsync();
        return request;
    }

    private static AuthenticatedUserDto ToCurrentUser(ApplicationUser user) =>
        new(user.Id, user.Username, user.DisplayName, user.Email, user.Role.ToString());

    // Listing — ownership and role scope

    [Fact]
    public async Task GetListAsync_WhenEmployee_ReturnsOnlyOwnRequests()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var otherEmployee = await CreateUserAsync("john.doe", UserRole.Employee);
        await CreateRequestAsync(category, employee, "Employee's own request");
        await CreateRequestAsync(category, otherEmployee, "Someone else's request");

        var result = await _sut.GetListAsync(new RequestListQuery(), ToCurrentUser(employee), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Employee's own request", result.Items[0].Title);
    }

    [Fact]
    public async Task GetListAsync_WhenSupportAgent_ReturnsAllRequests()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var otherEmployee = await CreateUserAsync("john.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        await CreateRequestAsync(category, employee);
        await CreateRequestAsync(category, otherEmployee);

        var result = await _sut.GetListAsync(new RequestListQuery(), ToCurrentUser(agent), CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task GetListAsync_WhenAdmin_ReturnsAllRequests()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var otherEmployee = await CreateUserAsync("john.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        await CreateRequestAsync(category, employee);
        await CreateRequestAsync(category, otherEmployee);

        var result = await _sut.GetListAsync(new RequestListQuery(), ToCurrentUser(admin), CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
    }

    // Details — ownership and role scope

    [Fact]
    public async Task GetByIdAsync_WhenEmployeeRequestsOwnRequest_ReturnsDetails()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var request = await CreateRequestAsync(category, employee);

        var details = await _sut.GetByIdAsync(request.Id, ToCurrentUser(employee), CancellationToken.None);

        Assert.Equal(request.Id, details.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WhenEmployeeRequestsAnotherUsersRequest_ThrowsNotFound()
    {
        var category = await CreateCategoryAsync("Hardware");
        var owner = await CreateUserAsync("jane.doe", UserRole.Employee);
        var otherEmployee = await CreateUserAsync("john.doe", UserRole.Employee);
        var request = await CreateRequestAsync(category, owner);

        await Assert.ThrowsAsync<SupportRequestNotFoundException>(
            () => _sut.GetByIdAsync(request.Id, ToCurrentUser(otherEmployee), CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_WhenSupportAgentRequestsAnotherUsersRequest_ReturnsDetails()
    {
        var category = await CreateCategoryAsync("Hardware");
        var owner = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, owner);

        var details = await _sut.GetByIdAsync(request.Id, ToCurrentUser(agent), CancellationToken.None);

        Assert.Equal(request.Id, details.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WhenAdminRequestsAnotherUsersRequest_ReturnsDetails()
    {
        var category = await CreateCategoryAsync("Hardware");
        var owner = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, owner);

        var details = await _sut.GetByIdAsync(request.Id, ToCurrentUser(admin), CancellationToken.None);

        Assert.Equal(request.Id, details.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WhenRequestDoesNotExist_ThrowsNotFound()
    {
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);

        await Assert.ThrowsAsync<SupportRequestNotFoundException>(
            () => _sut.GetByIdAsync(999_999, ToCurrentUser(admin), CancellationToken.None));
    }

    // Creation

    [Fact]
    public async Task CreateAsync_UsesAuthenticatedUserAsCreator()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var request = new CreateRequestRequest
        {
            Title = "Laptop does not start",
            Description = "The power button does not respond at all.",
            CategoryId = category.Id,
            Priority = RequestPriority.High,
        };

        var details = await _sut.CreateAsync(request, ToCurrentUser(employee), CancellationToken.None);

        Assert.Equal(employee.Id, details.CreatedBy.Id);
        Assert.Equal(RequestStatus.New, details.Status);
        Assert.Null(details.AssignedTo);
    }

    [Fact]
    public async Task CreateAsync_WhenCategoryDoesNotExist_ThrowsRequestCategoryNotFoundException()
    {
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var request = new CreateRequestRequest
        {
            Title = "Laptop does not start",
            Description = "The power button does not respond at all.",
            CategoryId = 999_999,
            Priority = RequestPriority.High,
        };

        await Assert.ThrowsAsync<RequestCategoryNotFoundException>(
            () => _sut.CreateAsync(request, ToCurrentUser(employee), CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WhenCategoryIsInactive_ThrowsRequestCategoryInactiveException()
    {
        var category = await CreateCategoryAsync("Hardware", isActive: false);
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var request = new CreateRequestRequest
        {
            Title = "Laptop does not start",
            Description = "The power button does not respond at all.",
            CategoryId = category.Id,
            Priority = RequestPriority.High,
        };

        await Assert.ThrowsAsync<RequestCategoryInactiveException>(
            () => _sut.CreateAsync(request, ToCurrentUser(employee), CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WhenCurrentUserIsInactive_ThrowsCurrentUserUnavailableException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var inactiveUser = await CreateUserAsync("jane.doe", UserRole.Employee, isActive: false);
        var request = new CreateRequestRequest
        {
            Title = "Laptop does not start",
            Description = "The power button does not respond at all.",
            CategoryId = category.Id,
            Priority = RequestPriority.High,
        };

        await Assert.ThrowsAsync<CurrentUserUnavailableException>(
            () => _sut.CreateAsync(request, ToCurrentUser(inactiveUser), CancellationToken.None));
    }

    // Filters

    [Fact]
    public async Task GetListAsync_FiltersByStatus()
    {
        var category = await CreateCategoryAsync("Hardware");
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        await CreateRequestAsync(category, admin, "Only new requests exist");

        var matching = await _sut.GetListAsync(
            new RequestListQuery { Status = RequestStatus.New }, ToCurrentUser(admin), CancellationToken.None);
        var nonMatching = await _sut.GetListAsync(
            new RequestListQuery { Status = RequestStatus.Resolved }, ToCurrentUser(admin), CancellationToken.None);

        Assert.Single(matching.Items);
        Assert.Empty(nonMatching.Items);
    }

    [Fact]
    public async Task GetListAsync_FiltersByPriority()
    {
        var category = await CreateCategoryAsync("Hardware");
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        await CreateRequestAsync(category, admin, priority: RequestPriority.Critical);

        var matching = await _sut.GetListAsync(
            new RequestListQuery { Priority = RequestPriority.Critical }, ToCurrentUser(admin), CancellationToken.None);
        var nonMatching = await _sut.GetListAsync(
            new RequestListQuery { Priority = RequestPriority.Low }, ToCurrentUser(admin), CancellationToken.None);

        Assert.Single(matching.Items);
        Assert.Empty(nonMatching.Items);
    }

    [Fact]
    public async Task GetListAsync_FiltersByCategory()
    {
        var hardware = await CreateCategoryAsync("Hardware");
        var software = await CreateCategoryAsync("Software");
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        await CreateRequestAsync(hardware, admin, "Hardware issue");
        await CreateRequestAsync(software, admin, "Software issue");

        var result = await _sut.GetListAsync(
            new RequestListQuery { CategoryId = hardware.Id }, ToCurrentUser(admin), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Hardware issue", result.Items[0].Title);
    }

    // Pagination and ordering

    [Fact]
    public async Task GetListAsync_PaginationMetadataIsCorrect()
    {
        var category = await CreateCategoryAsync("Hardware");
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        for (var i = 0; i < 5; i++)
        {
            await CreateRequestAsync(category, admin, $"Request {i}");
        }

        var result = await _sut.GetListAsync(
            new RequestListQuery { Page = 2, PageSize = 2 }, ToCurrentUser(admin), CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task GetListAsync_WhenEmptyResult_ReturnsEmptyItemsWithZeroTotalPages()
    {
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);

        var result = await _sut.GetListAsync(new RequestListQuery(), ToCurrentUser(admin), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetListAsync_OrdersByNewestCreatedAtThenHighestIdFirst()
    {
        var category = await CreateCategoryAsync("Hardware");
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var first = await CreateRequestAsync(category, admin, "First");
        var second = await CreateRequestAsync(category, admin, "Second");
        var third = await CreateRequestAsync(category, admin, "Third");

        var result = await _sut.GetListAsync(new RequestListQuery(), ToCurrentUser(admin), CancellationToken.None);

        Assert.Equal([third.Id, second.Id, first.Id], result.Items.Select(item => item.Id));
    }
}
