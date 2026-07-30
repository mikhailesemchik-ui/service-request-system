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

    // Assignment

    private async Task PreAssignAsync(SupportRequest request, ApplicationUser assignee)
    {
        request.AssignTo(assignee);
        await _dbContext.SaveChangesAsync();
    }

    private async Task PreChangeStatusAsync(SupportRequest request, RequestStatus status)
    {
        request.ChangeStatus(status);
        await _dbContext.SaveChangesAsync();
    }

    private async Task<int> CountHistoryAsync(int requestId)
    {
        await using var verifyContext = CreateContext();
        return await verifyContext.RequestHistory.CountAsync(h => h.ServiceRequestId == requestId);
    }

    [Fact]
    public async Task SetAssignmentAsync_WhenEmployee_ThrowsRequestManagementForbiddenException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);

        await Assert.ThrowsAsync<RequestManagementForbiddenException>(() => _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = agent.Id }, ToCurrentUser(employee), CancellationToken.None));
    }

    [Fact]
    public async Task SetAssignmentAsync_SupportAgentAssignsUnassignedRequestToSelf_Succeeds()
    {
        var category = await CreateCategoryAsync("Hardware");
        var creator = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, creator);

        var details = await _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = agent.Id }, ToCurrentUser(agent), CancellationToken.None);

        Assert.Equal(agent.Id, details.AssignedTo?.Id);
    }

    [Fact]
    public async Task SetAssignmentAsync_SupportAgentAssignsToAnotherUser_ThrowsRequestManagementForbiddenException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var creator = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var otherAgent = await CreateUserAsync("agent.jones", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, creator);

        await Assert.ThrowsAsync<RequestManagementForbiddenException>(() => _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = otherAgent.Id }, ToCurrentUser(agent), CancellationToken.None));
    }

    [Fact]
    public async Task SetAssignmentAsync_SupportAgentTriesToTakeOverAnotherAgentsRequest_ThrowsRequestManagementForbiddenException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var creator = await CreateUserAsync("jane.doe", UserRole.Employee);
        var firstAgent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var secondAgent = await CreateUserAsync("agent.jones", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, creator);
        await PreAssignAsync(request, firstAgent);

        await Assert.ThrowsAsync<RequestManagementForbiddenException>(() => _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = secondAgent.Id }, ToCurrentUser(secondAgent), CancellationToken.None));
    }

    [Fact]
    public async Task SetAssignmentAsync_SupportAgentRemovesOwnAssignment_Succeeds()
    {
        var category = await CreateCategoryAsync("Hardware");
        var creator = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, creator);
        await PreAssignAsync(request, agent);

        var details = await _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = null }, ToCurrentUser(agent), CancellationToken.None);

        Assert.Null(details.AssignedTo);
    }

    [Fact]
    public async Task SetAssignmentAsync_SupportAgentTriesToRemoveAnotherUsersAssignment_ThrowsRequestManagementForbiddenException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var creator = await CreateUserAsync("jane.doe", UserRole.Employee);
        var firstAgent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var secondAgent = await CreateUserAsync("agent.jones", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, creator);
        await PreAssignAsync(request, firstAgent);

        await Assert.ThrowsAsync<RequestManagementForbiddenException>(() => _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = null }, ToCurrentUser(secondAgent), CancellationToken.None));
    }

    [Fact]
    public async Task SetAssignmentAsync_AdminAssignsToSupportAgent_Succeeds()
    {
        var category = await CreateCategoryAsync("Hardware");
        var creator = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, creator);

        var details = await _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = agent.Id }, ToCurrentUser(admin), CancellationToken.None);

        Assert.Equal(agent.Id, details.AssignedTo?.Id);
    }

    [Fact]
    public async Task SetAssignmentAsync_AdminAssignsToAdmin_Succeeds()
    {
        var category = await CreateCategoryAsync("Hardware");
        var creator = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var otherAdmin = await CreateUserAsync("second.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, creator);

        var details = await _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = otherAdmin.Id }, ToCurrentUser(admin), CancellationToken.None);

        Assert.Equal(otherAdmin.Id, details.AssignedTo?.Id);
    }

    [Fact]
    public async Task SetAssignmentAsync_AdminAssignsToEmployee_ThrowsInvalidRequestAssigneeException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var creator = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var otherEmployee = await CreateUserAsync("john.doe", UserRole.Employee);
        var request = await CreateRequestAsync(category, creator);

        await Assert.ThrowsAsync<InvalidRequestAssigneeException>(() => _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = otherEmployee.Id }, ToCurrentUser(admin), CancellationToken.None));
    }

    [Fact]
    public async Task SetAssignmentAsync_AdminAssignsToInactiveSupportAgent_ThrowsInvalidRequestAssigneeException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var creator = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var inactiveAgent = await CreateUserAsync("agent.smith", UserRole.SupportAgent, isActive: false);
        var request = await CreateRequestAsync(category, creator);

        await Assert.ThrowsAsync<InvalidRequestAssigneeException>(() => _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = inactiveAgent.Id }, ToCurrentUser(admin), CancellationToken.None));
    }

    [Fact]
    public async Task SetAssignmentAsync_AdminReassigns_Succeeds()
    {
        var category = await CreateCategoryAsync("Hardware");
        var creator = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var firstAgent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var secondAgent = await CreateUserAsync("agent.jones", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, creator);
        await PreAssignAsync(request, firstAgent);

        var details = await _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = secondAgent.Id }, ToCurrentUser(admin), CancellationToken.None);

        Assert.Equal(secondAgent.Id, details.AssignedTo?.Id);
    }

    [Fact]
    public async Task SetAssignmentAsync_AdminRemovesAssignment_Succeeds()
    {
        var category = await CreateCategoryAsync("Hardware");
        var creator = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, creator);
        await PreAssignAsync(request, agent);

        var details = await _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = null }, ToCurrentUser(admin), CancellationToken.None);

        Assert.Null(details.AssignedTo);
    }

    [Fact]
    public async Task SetAssignmentAsync_WhenRequestIsClosed_ThrowsInvalidRequestAssigneeException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var creator = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var otherAgent = await CreateUserAsync("agent.jones", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, creator);
        await PreAssignAsync(request, agent);
        await PreChangeStatusAsync(request, RequestStatus.InProgress);
        await PreChangeStatusAsync(request, RequestStatus.Resolved);
        await PreChangeStatusAsync(request, RequestStatus.Closed);

        await Assert.ThrowsAsync<InvalidRequestAssigneeException>(() => _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = otherAgent.Id }, ToCurrentUser(admin), CancellationToken.None));
    }

    [Fact]
    public async Task SetAssignmentAsync_WhenRequestIsCancelled_ThrowsInvalidRequestAssigneeException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var creator = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, creator);
        await PreChangeStatusAsync(request, RequestStatus.Cancelled);

        await Assert.ThrowsAsync<InvalidRequestAssigneeException>(() => _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = agent.Id }, ToCurrentUser(admin), CancellationToken.None));
    }

    [Fact]
    public async Task SetAssignmentAsync_IdempotentSameAssignment_CreatesNoDuplicateHistory()
    {
        var category = await CreateCategoryAsync("Hardware");
        var creator = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, creator);

        await _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = agent.Id }, ToCurrentUser(admin), CancellationToken.None);
        await _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = agent.Id }, ToCurrentUser(admin), CancellationToken.None);

        Assert.Equal(1, await CountHistoryAsync(request.Id));
    }

    // Status transitions

    [Fact]
    public async Task ChangeStatusAsync_EmployeeCancelsOwnNewRequest_Succeeds()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var request = await CreateRequestAsync(category, employee);

        var details = await _sut.ChangeStatusAsync(
            request.Id, new UpdateRequestStatusRequest { Status = RequestStatus.Cancelled }, ToCurrentUser(employee), CancellationToken.None);

        Assert.Equal(RequestStatus.Cancelled, details.Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_EmployeeCancelsOwnInProgressRequest_Succeeds()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);
        await PreAssignAsync(request, agent);
        await PreChangeStatusAsync(request, RequestStatus.InProgress);

        var details = await _sut.ChangeStatusAsync(
            request.Id, new UpdateRequestStatusRequest { Status = RequestStatus.Cancelled }, ToCurrentUser(employee), CancellationToken.None);

        Assert.Equal(RequestStatus.Cancelled, details.Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_EmployeeClosesOwnResolvedRequest_Succeeds()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);
        await PreAssignAsync(request, agent);
        await PreChangeStatusAsync(request, RequestStatus.InProgress);
        await PreChangeStatusAsync(request, RequestStatus.Resolved);

        var details = await _sut.ChangeStatusAsync(
            request.Id, new UpdateRequestStatusRequest { Status = RequestStatus.Closed }, ToCurrentUser(employee), CancellationToken.None);

        Assert.Equal(RequestStatus.Closed, details.Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_EmployeeCannotResolve_ThrowsRequestManagementForbiddenException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);
        await PreAssignAsync(request, agent);
        await PreChangeStatusAsync(request, RequestStatus.InProgress);

        await Assert.ThrowsAsync<RequestManagementForbiddenException>(() => _sut.ChangeStatusAsync(
            request.Id, new UpdateRequestStatusRequest { Status = RequestStatus.Resolved }, ToCurrentUser(employee), CancellationToken.None));
    }

    [Fact]
    public async Task ChangeStatusAsync_EmployeeCannotCloseNewRequest_ThrowsInvalidRequestStatusTransitionException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var request = await CreateRequestAsync(category, employee);

        await Assert.ThrowsAsync<InvalidRequestStatusTransitionException>(() => _sut.ChangeStatusAsync(
            request.Id, new UpdateRequestStatusRequest { Status = RequestStatus.Closed }, ToCurrentUser(employee), CancellationToken.None));
    }

    [Fact]
    public async Task ChangeStatusAsync_EmployeeCannotChangeAnotherUsersRequest_ThrowsSupportRequestNotFoundException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var owner = await CreateUserAsync("jane.doe", UserRole.Employee);
        var otherEmployee = await CreateUserAsync("john.doe", UserRole.Employee);
        var request = await CreateRequestAsync(category, owner);

        await Assert.ThrowsAsync<SupportRequestNotFoundException>(() => _sut.ChangeStatusAsync(
            request.Id, new UpdateRequestStatusRequest { Status = RequestStatus.Cancelled }, ToCurrentUser(otherEmployee), CancellationToken.None));
    }

    [Fact]
    public async Task ChangeStatusAsync_SupportAgentChangesAssignedRequestToInProgress_Succeeds()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);
        await PreAssignAsync(request, agent);

        var details = await _sut.ChangeStatusAsync(
            request.Id, new UpdateRequestStatusRequest { Status = RequestStatus.InProgress }, ToCurrentUser(agent), CancellationToken.None);

        Assert.Equal(RequestStatus.InProgress, details.Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_SupportAgentCannotChangeUnassignedRequest_ThrowsRequestManagementForbiddenException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);

        await Assert.ThrowsAsync<RequestManagementForbiddenException>(() => _sut.ChangeStatusAsync(
            request.Id, new UpdateRequestStatusRequest { Status = RequestStatus.InProgress }, ToCurrentUser(agent), CancellationToken.None));
    }

    [Fact]
    public async Task ChangeStatusAsync_SupportAgentCannotChangeAnotherAgentsRequest_ThrowsRequestManagementForbiddenException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var firstAgent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var secondAgent = await CreateUserAsync("agent.jones", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);
        await PreAssignAsync(request, firstAgent);

        await Assert.ThrowsAsync<RequestManagementForbiddenException>(() => _sut.ChangeStatusAsync(
            request.Id, new UpdateRequestStatusRequest { Status = RequestStatus.InProgress }, ToCurrentUser(secondAgent), CancellationToken.None));
    }

    [Fact]
    public async Task ChangeStatusAsync_SupportAgentResolvesAssignedRequest_Succeeds()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);
        await PreAssignAsync(request, agent);
        await PreChangeStatusAsync(request, RequestStatus.InProgress);

        var details = await _sut.ChangeStatusAsync(
            request.Id, new UpdateRequestStatusRequest { Status = RequestStatus.Resolved }, ToCurrentUser(agent), CancellationToken.None);

        Assert.Equal(RequestStatus.Resolved, details.Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_SupportAgentReopensResolvedRequest_Succeeds()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);
        await PreAssignAsync(request, agent);
        await PreChangeStatusAsync(request, RequestStatus.InProgress);
        await PreChangeStatusAsync(request, RequestStatus.Resolved);

        var details = await _sut.ChangeStatusAsync(
            request.Id, new UpdateRequestStatusRequest { Status = RequestStatus.InProgress }, ToCurrentUser(agent), CancellationToken.None);

        Assert.Equal(RequestStatus.InProgress, details.Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_SupportAgentCannotClose_ThrowsRequestManagementForbiddenException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);
        await PreAssignAsync(request, agent);
        await PreChangeStatusAsync(request, RequestStatus.InProgress);
        await PreChangeStatusAsync(request, RequestStatus.Resolved);

        await Assert.ThrowsAsync<RequestManagementForbiddenException>(() => _sut.ChangeStatusAsync(
            request.Id, new UpdateRequestStatusRequest { Status = RequestStatus.Closed }, ToCurrentUser(agent), CancellationToken.None));
    }

    [Fact]
    public async Task ChangeStatusAsync_AdminPerformsEveryValidTransitionAlongTheFullLifecycle_Succeeds()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);
        await PreAssignAsync(request, agent);
        var currentUser = ToCurrentUser(admin);

        async Task<RequestStatus> ChangeAsync(RequestStatus status)
        {
            var details = await _sut.ChangeStatusAsync(
                request.Id, new UpdateRequestStatusRequest { Status = status }, currentUser, CancellationToken.None);
            return details.Status;
        }

        Assert.Equal(RequestStatus.InProgress, await ChangeAsync(RequestStatus.InProgress));
        Assert.Equal(RequestStatus.WaitingForUser, await ChangeAsync(RequestStatus.WaitingForUser));
        Assert.Equal(RequestStatus.InProgress, await ChangeAsync(RequestStatus.InProgress));
        Assert.Equal(RequestStatus.Resolved, await ChangeAsync(RequestStatus.Resolved));
        Assert.Equal(RequestStatus.Closed, await ChangeAsync(RequestStatus.Closed));
    }

    [Fact]
    public async Task ChangeStatusAsync_AdminCancelsInProgressRequest_Succeeds()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);
        await PreAssignAsync(request, agent);
        await PreChangeStatusAsync(request, RequestStatus.InProgress);

        var details = await _sut.ChangeStatusAsync(
            request.Id, new UpdateRequestStatusRequest { Status = RequestStatus.Cancelled }, ToCurrentUser(admin), CancellationToken.None);

        Assert.Equal(RequestStatus.Cancelled, details.Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_AdminCannotPerformInvalidTransition_ThrowsInvalidRequestStatusTransitionException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee);

        await Assert.ThrowsAsync<InvalidRequestStatusTransitionException>(() => _sut.ChangeStatusAsync(
            request.Id, new UpdateRequestStatusRequest { Status = RequestStatus.Resolved }, ToCurrentUser(admin), CancellationToken.None));
    }

    [Fact]
    public async Task ChangeStatusAsync_ToInProgressWithoutAssignee_ThrowsInvalidRequestStatusTransitionException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee);

        await Assert.ThrowsAsync<InvalidRequestStatusTransitionException>(() => _sut.ChangeStatusAsync(
            request.Id, new UpdateRequestStatusRequest { Status = RequestStatus.InProgress }, ToCurrentUser(admin), CancellationToken.None));
    }

    [Fact]
    public async Task ChangeStatusAsync_SameStatus_CreatesNoHistory()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var request = await CreateRequestAsync(category, employee);

        await _sut.ChangeStatusAsync(
            request.Id, new UpdateRequestStatusRequest { Status = RequestStatus.New }, ToCurrentUser(employee), CancellationToken.None);

        Assert.Equal(0, await CountHistoryAsync(request.Id));
    }

    // History

    [Fact]
    public async Task GetHistoryAsync_AssignmentChange_PersistsHistoryEntry()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);

        await _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = agent.Id }, ToCurrentUser(admin), CancellationToken.None);

        var history = await _sut.GetHistoryAsync(request.Id, ToCurrentUser(admin), CancellationToken.None);

        var entry = Assert.Single(history);
        Assert.Equal(RequestHistoryActions.AssignmentChanged, entry.Action);
        Assert.Null(entry.PreviousValue);
        Assert.Equal(agent.Id.ToString(), entry.NewValue);
        Assert.Equal("agent.smith Display", entry.NewDisplayValue);
    }

    [Fact]
    public async Task GetHistoryAsync_Reassignment_StoresPreviousAndNewIds()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var firstAgent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var secondAgent = await CreateUserAsync("agent.jones", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);
        await PreAssignAsync(request, firstAgent);

        await _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = secondAgent.Id }, ToCurrentUser(admin), CancellationToken.None);

        var history = await _sut.GetHistoryAsync(request.Id, ToCurrentUser(admin), CancellationToken.None);

        var entry = Assert.Single(history);
        Assert.Equal(firstAgent.Id.ToString(), entry.PreviousValue);
        Assert.Equal(secondAgent.Id.ToString(), entry.NewValue);
        Assert.Equal("agent.smith Display", entry.PreviousDisplayValue);
        Assert.Equal("agent.jones Display", entry.NewDisplayValue);
    }

    [Fact]
    public async Task GetHistoryAsync_AssignmentRemoval_StoresNullNewValue()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);
        await PreAssignAsync(request, agent);

        await _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = null }, ToCurrentUser(admin), CancellationToken.None);

        var history = await _sut.GetHistoryAsync(request.Id, ToCurrentUser(admin), CancellationToken.None);

        var entry = Assert.Single(history);
        Assert.Null(entry.NewValue);
        Assert.Null(entry.NewDisplayValue);
    }

    [Fact]
    public async Task GetHistoryAsync_StatusChange_StoresEnumNames()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var request = await CreateRequestAsync(category, employee);

        await _sut.ChangeStatusAsync(
            request.Id, new UpdateRequestStatusRequest { Status = RequestStatus.Cancelled }, ToCurrentUser(employee), CancellationToken.None);

        var history = await _sut.GetHistoryAsync(request.Id, ToCurrentUser(employee), CancellationToken.None);

        var entry = Assert.Single(history);
        Assert.Equal(RequestHistoryActions.StatusChanged, entry.Action);
        Assert.Equal("New", entry.PreviousValue);
        Assert.Equal("Cancelled", entry.NewValue);
        Assert.Equal("New", entry.PreviousDisplayValue);
        Assert.Equal("Cancelled", entry.NewDisplayValue);
    }

    [Fact]
    public async Task GetHistoryAsync_ActorIsCurrentUser()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var request = await CreateRequestAsync(category, employee);

        await _sut.ChangeStatusAsync(
            request.Id, new UpdateRequestStatusRequest { Status = RequestStatus.Cancelled }, ToCurrentUser(employee), CancellationToken.None);

        var history = await _sut.GetHistoryAsync(request.Id, ToCurrentUser(employee), CancellationToken.None);

        Assert.Equal(employee.Id, Assert.Single(history).ChangedBy.Id);
    }

    [Fact]
    public async Task GetHistoryAsync_OrdersOldestFirst()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);

        await _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = agent.Id }, ToCurrentUser(admin), CancellationToken.None);
        await _sut.ChangeStatusAsync(
            request.Id, new UpdateRequestStatusRequest { Status = RequestStatus.InProgress }, ToCurrentUser(agent), CancellationToken.None);

        var history = await _sut.GetHistoryAsync(request.Id, ToCurrentUser(admin), CancellationToken.None);

        Assert.Equal(
            [RequestHistoryActions.AssignmentChanged, RequestHistoryActions.StatusChanged],
            history.Select(entry => entry.Action));
    }

    [Fact]
    public async Task GetHistoryAsync_WhenEmpty_ReturnsEmptyList()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var request = await CreateRequestAsync(category, employee);

        var history = await _sut.GetHistoryAsync(request.Id, ToCurrentUser(employee), CancellationToken.None);

        Assert.Empty(history);
    }

    [Fact]
    public async Task GetHistoryAsync_WhenEmployeeRequestsAnotherUsersHistory_ThrowsSupportRequestNotFoundException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var owner = await CreateUserAsync("jane.doe", UserRole.Employee);
        var otherEmployee = await CreateUserAsync("john.doe", UserRole.Employee);
        var request = await CreateRequestAsync(category, owner);

        await Assert.ThrowsAsync<SupportRequestNotFoundException>(() =>
            _sut.GetHistoryAsync(request.Id, ToCurrentUser(otherEmployee), CancellationToken.None));
    }

    [Fact]
    public async Task GetHistoryAsync_SupportAgentAndAdminCanAccessAnyRequestsHistory()
    {
        var category = await CreateCategoryAsync("Hardware");
        var owner = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, owner);

        var agentHistory = await _sut.GetHistoryAsync(request.Id, ToCurrentUser(agent), CancellationToken.None);
        var adminHistory = await _sut.GetHistoryAsync(request.Id, ToCurrentUser(admin), CancellationToken.None);

        Assert.NotNull(agentHistory);
        Assert.NotNull(adminHistory);
    }

    [Fact]
    public async Task SetAssignmentAsync_IdempotentOperation_CreatesNoHistoryOnSecondCall()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);

        await _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = agent.Id }, ToCurrentUser(agent), CancellationToken.None);
        await _sut.SetAssignmentAsync(
            request.Id, new UpdateRequestAssignmentRequest { AssignedToUserId = agent.Id }, ToCurrentUser(agent), CancellationToken.None);

        var history = await _sut.GetHistoryAsync(request.Id, ToCurrentUser(agent), CancellationToken.None);

        Assert.Single(history);
    }

    // Eligible assignees

    [Fact]
    public async Task GetEligibleAssigneesAsync_ReturnsOnlyActiveSupportAgentsAndAdmins()
    {
        var activeAgent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var activeAdmin = await CreateUserAsync("root.admin", UserRole.Admin);
        await CreateUserAsync("john.doe", UserRole.Employee);
        await CreateUserAsync("inactive.agent", UserRole.SupportAgent, isActive: false);

        var assignees = await _sut.GetEligibleAssigneesAsync(CancellationToken.None);

        Assert.Equal(
            new[] { activeAdmin.Id, activeAgent.Id }.OrderBy(id => id),
            assignees.Select(a => a.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task GetEligibleAssigneesAsync_ExcludesEmployees()
    {
        await CreateUserAsync("john.doe", UserRole.Employee);

        var assignees = await _sut.GetEligibleAssigneesAsync(CancellationToken.None);

        Assert.DoesNotContain(assignees, a => a.Role == UserRole.Employee);
    }

    [Fact]
    public async Task GetEligibleAssigneesAsync_ExcludesInactiveUsers()
    {
        var inactiveAgent = await CreateUserAsync("inactive.agent", UserRole.SupportAgent, isActive: false);

        var assignees = await _sut.GetEligibleAssigneesAsync(CancellationToken.None);

        Assert.DoesNotContain(assignees, a => a.Id == inactiveAgent.Id);
    }

    [Fact]
    public async Task GetEligibleAssigneesAsync_OrdersByDisplayName()
    {
        await CreateUserAsync("zzz.agent", UserRole.SupportAgent);
        await CreateUserAsync("aaa.agent", UserRole.SupportAgent);

        var assignees = await _sut.GetEligibleAssigneesAsync(CancellationToken.None);

        var orderedNames = assignees.Select(a => a.DisplayName).ToList();
        Assert.Equal(orderedNames.OrderBy(name => name, StringComparer.Ordinal), orderedNames);
    }
}
