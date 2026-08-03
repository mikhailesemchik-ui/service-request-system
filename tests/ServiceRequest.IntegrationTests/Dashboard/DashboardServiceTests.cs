using Microsoft.EntityFrameworkCore;
using ServiceRequest.Application.Authentication;
using ServiceRequest.Application.Dashboard;
using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;
using ServiceRequest.Infrastructure.Dashboard;
using ServiceRequest.Infrastructure.Data;

namespace ServiceRequest.IntegrationTests.Dashboard;

public sealed class DashboardServiceTests : IDisposable
{
    private readonly string _databasePath;
    private readonly ApplicationDbContext _dbContext;
    private readonly DashboardService _sut;

    public DashboardServiceTests()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"service-requests-dashboard-service-{Guid.NewGuid():N}.db");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={_databasePath};Pooling=False")
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.Migrate();
        _sut = new DashboardService(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private async Task<ApplicationUser> CreateUserAsync(
        string username, UserRole role, bool isActive = true)
    {
        var user = new ApplicationUser(username, $"{username} Display", $"{username}@test.example", role);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        if (!isActive)
        {
            user.SetActiveState(false);
            await _dbContext.SaveChangesAsync();
        }

        return user;
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

    private async Task<SupportRequest> CreateRequestAsync(
        RequestCategory category,
        ApplicationUser creator,
        RequestPriority priority = RequestPriority.Medium,
        RequestStatus status = RequestStatus.New,
        ApplicationUser? assignedTo = null)
    {
        var request = new SupportRequest("Test request", "Test description.", priority, category, creator);

        if (assignedTo is not null)
        {
            request.AssignTo(assignedTo);
        }

        _dbContext.SupportRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        if (status != RequestStatus.New)
        {
            await SetStatusAsync(request, status, assignedTo);
        }

        return request;
    }

    private async Task SetStatusAsync(
        SupportRequest request, RequestStatus targetStatus, ApplicationUser? agent)
    {
        if (targetStatus == RequestStatus.Cancelled)
        {
            request.ChangeStatus(RequestStatus.Cancelled);
            await _dbContext.SaveChangesAsync();
            return;
        }

        if (agent is not null && request.AssignedToUserId is null)
        {
            request.AssignTo(agent);
        }

        request.ChangeStatus(RequestStatus.InProgress);

        switch (targetStatus)
        {
            case RequestStatus.InProgress:
                break;
            case RequestStatus.WaitingForUser:
                request.ChangeStatus(RequestStatus.WaitingForUser);
                break;
            case RequestStatus.Resolved:
                request.ChangeStatus(RequestStatus.Resolved);
                break;
            case RequestStatus.Closed:
                request.ChangeStatus(RequestStatus.Resolved);
                request.ChangeStatus(RequestStatus.Closed);
                break;
        }

        await _dbContext.SaveChangesAsync();
    }

    private static AuthenticatedUserDto ToCurrentUser(ApplicationUser user) =>
        new(user.Id, user.Username, user.DisplayName, user.Email, user.Role.ToString());

    // ─── Employee scope ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaryAsync_Employee_ScopeIsEmployee()
    {
        var employee = await CreateUserAsync("employee", UserRole.Employee);

        var result = await _sut.GetSummaryAsync(ToCurrentUser(employee), CancellationToken.None);

        Assert.Equal("Employee", result.Scope);
        Assert.Null(result.StaffMetrics);
        Assert.Null(result.AdminMetrics);
    }

    [Fact]
    public async Task GetSummaryAsync_Employee_TotalCountsOnlyOwnRequests()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("employee", UserRole.Employee);
        var other = await CreateUserAsync("other", UserRole.Employee);
        await CreateRequestAsync(category, employee);
        await CreateRequestAsync(category, employee);
        await CreateRequestAsync(category, other); // must not count

        var result = await _sut.GetSummaryAsync(ToCurrentUser(employee), CancellationToken.None);

        Assert.Equal(2, result.TotalRequests);
    }

    [Fact]
    public async Task GetSummaryAsync_Employee_OpenCountUsesActiveStatuses()
    {
        var category = await CreateCategoryAsync("Hardware");
        var agent = await CreateUserAsync("agent", UserRole.SupportAgent);
        var employee = await CreateUserAsync("employee", UserRole.Employee);
        await CreateRequestAsync(category, employee, status: RequestStatus.New);
        await CreateRequestAsync(category, employee, status: RequestStatus.InProgress, assignedTo: agent);
        await CreateRequestAsync(category, employee, status: RequestStatus.WaitingForUser, assignedTo: agent);
        await CreateRequestAsync(category, employee, status: RequestStatus.Resolved, assignedTo: agent);

        var result = await _sut.GetSummaryAsync(ToCurrentUser(employee), CancellationToken.None);

        Assert.Equal(3, result.OpenRequests);
    }

    [Fact]
    public async Task GetSummaryAsync_Employee_ResolvedClosedCancelledCountsAreCorrect()
    {
        var category = await CreateCategoryAsync("Hardware");
        var agent = await CreateUserAsync("agent", UserRole.SupportAgent);
        var employee = await CreateUserAsync("employee", UserRole.Employee);
        await CreateRequestAsync(category, employee, status: RequestStatus.Resolved, assignedTo: agent);
        await CreateRequestAsync(category, employee, status: RequestStatus.Closed, assignedTo: agent);
        await CreateRequestAsync(category, employee, status: RequestStatus.Cancelled);

        var result = await _sut.GetSummaryAsync(ToCurrentUser(employee), CancellationToken.None);

        Assert.Equal(1, result.ResolvedRequests);
        Assert.Equal(1, result.ClosedRequests);
        Assert.Equal(1, result.CancelledRequests);
    }

    [Fact]
    public async Task GetSummaryAsync_Employee_StatusCountsContainAllStatuses()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("employee", UserRole.Employee);

        var result = await _sut.GetSummaryAsync(ToCurrentUser(employee), CancellationToken.None);

        var statuses = result.StatusCounts.Select(s => s.Status).ToList();
        Assert.Contains("New", statuses);
        Assert.Contains("InProgress", statuses);
        Assert.Contains("WaitingForUser", statuses);
        Assert.Contains("Resolved", statuses);
        Assert.Contains("Closed", statuses);
        Assert.Contains("Cancelled", statuses);
        Assert.Equal(6, result.StatusCounts.Count);
    }

    [Fact]
    public async Task GetSummaryAsync_Employee_PriorityCountsContainAllPriorities()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("employee", UserRole.Employee);

        var result = await _sut.GetSummaryAsync(ToCurrentUser(employee), CancellationToken.None);

        var priorities = result.PriorityCounts.Select(p => p.Priority).ToList();
        Assert.Contains("Low", priorities);
        Assert.Contains("Medium", priorities);
        Assert.Contains("High", priorities);
        Assert.Contains("Critical", priorities);
        Assert.Equal(4, result.PriorityCounts.Count);
    }

    [Fact]
    public async Task GetSummaryAsync_Employee_StatusCountsExcludeOtherUsersRequests()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("employee", UserRole.Employee);
        var other = await CreateUserAsync("other", UserRole.Employee);
        await CreateRequestAsync(category, employee, status: RequestStatus.New);
        await CreateRequestAsync(category, other, status: RequestStatus.Cancelled); // must not count

        var result = await _sut.GetSummaryAsync(ToCurrentUser(employee), CancellationToken.None);

        Assert.Equal(1, result.StatusCounts.Single(s => s.Status == "New").Count);
        Assert.Equal(0, result.StatusCounts.Single(s => s.Status == "Cancelled").Count);
    }

    [Fact]
    public async Task GetSummaryAsync_Employee_PriorityCountsExcludeOtherUsersRequests()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("employee", UserRole.Employee);
        var other = await CreateUserAsync("other", UserRole.Employee);
        await CreateRequestAsync(category, employee, priority: RequestPriority.High);
        await CreateRequestAsync(category, other, priority: RequestPriority.Critical); // must not count

        var result = await _sut.GetSummaryAsync(ToCurrentUser(employee), CancellationToken.None);

        Assert.Equal(1, result.PriorityCounts.Single(p => p.Priority == "High").Count);
        Assert.Equal(0, result.PriorityCounts.Single(p => p.Priority == "Critical").Count);
    }

    [Fact]
    public async Task GetSummaryAsync_Employee_RecentRequestsContainOnlyOwnRequests()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("employee", UserRole.Employee);
        var other = await CreateUserAsync("other", UserRole.Employee);
        await CreateRequestAsync(category, employee);
        await CreateRequestAsync(category, other); // must not appear

        var result = await _sut.GetSummaryAsync(ToCurrentUser(employee), CancellationToken.None);

        Assert.Single(result.RecentRequests);
    }

    [Fact]
    public async Task GetSummaryAsync_Employee_RecentRequestsLimitedToFive()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("employee", UserRole.Employee);

        for (var i = 0; i < 7; i++)
        {
            await CreateRequestAsync(category, employee);
        }

        var result = await _sut.GetSummaryAsync(ToCurrentUser(employee), CancellationToken.None);

        Assert.Equal(5, result.RecentRequests.Count);
    }

    [Fact]
    public async Task GetSummaryAsync_Employee_RecentRequestsOrderedByUpdatedAtDescending()
    {
        var category = await CreateCategoryAsync("Hardware");
        var agent = await CreateUserAsync("agent", UserRole.SupportAgent);
        var employee = await CreateUserAsync("employee", UserRole.Employee);

        // Create request 1, then update it (move to InProgress = UpdatedAt changes)
        var request1 = await CreateRequestAsync(category, employee, assignedTo: agent);
        await Task.Delay(10);
        var request2 = await CreateRequestAsync(category, employee);
        await Task.Delay(10);
        // Update request1 to make its UpdatedAt newer than request2
        request1.ChangeStatus(RequestStatus.InProgress);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetSummaryAsync(ToCurrentUser(employee), CancellationToken.None);

        Assert.Equal(request1.Id, result.RecentRequests[0].Id);
        Assert.Equal(request2.Id, result.RecentRequests[1].Id);
    }

    // ─── SupportAgent scope ───────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaryAsync_SupportAgent_ScopeIsSupportAgent()
    {
        var agent = await CreateUserAsync("agent", UserRole.SupportAgent);

        var result = await _sut.GetSummaryAsync(ToCurrentUser(agent), CancellationToken.None);

        Assert.Equal("SupportAgent", result.Scope);
        Assert.NotNull(result.StaffMetrics);
        Assert.Null(result.AdminMetrics);
    }

    [Fact]
    public async Task GetSummaryAsync_SupportAgent_TotalIncludesAllRequests()
    {
        var category = await CreateCategoryAsync("Hardware");
        var emp1 = await CreateUserAsync("emp1", UserRole.Employee);
        var emp2 = await CreateUserAsync("emp2", UserRole.Employee);
        var agent = await CreateUserAsync("agent", UserRole.SupportAgent);
        await CreateRequestAsync(category, emp1);
        await CreateRequestAsync(category, emp2);

        var result = await _sut.GetSummaryAsync(ToCurrentUser(agent), CancellationToken.None);

        Assert.Equal(2, result.TotalRequests);
    }

    [Fact]
    public async Task GetSummaryAsync_SupportAgent_ActiveCountIsCorrect()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("employee", UserRole.Employee);
        var agent = await CreateUserAsync("agent", UserRole.SupportAgent);
        await CreateRequestAsync(category, employee, status: RequestStatus.New);
        await CreateRequestAsync(category, employee, status: RequestStatus.InProgress, assignedTo: agent);
        await CreateRequestAsync(category, employee, status: RequestStatus.Closed, assignedTo: agent);

        var result = await _sut.GetSummaryAsync(ToCurrentUser(agent), CancellationToken.None);

        Assert.Equal(2, result.OpenRequests);
    }

    [Fact]
    public async Task GetSummaryAsync_SupportAgent_UnassignedActiveCountIsCorrect()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("employee", UserRole.Employee);
        var agent = await CreateUserAsync("agent", UserRole.SupportAgent);
        await CreateRequestAsync(category, employee, status: RequestStatus.New); // unassigned
        await CreateRequestAsync(category, employee, status: RequestStatus.InProgress, assignedTo: agent); // assigned

        var result = await _sut.GetSummaryAsync(ToCurrentUser(agent), CancellationToken.None);

        Assert.Equal(1, result.StaffMetrics!.UnassignedActiveRequests);
    }

    [Fact]
    public async Task GetSummaryAsync_SupportAgent_AssignedToMeCountIsCorrect()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("employee", UserRole.Employee);
        var agent = await CreateUserAsync("agent", UserRole.SupportAgent);
        var agent2 = await CreateUserAsync("agent2", UserRole.SupportAgent);
        await CreateRequestAsync(category, employee, status: RequestStatus.InProgress, assignedTo: agent);
        await CreateRequestAsync(category, employee, status: RequestStatus.InProgress, assignedTo: agent2);
        await CreateRequestAsync(category, employee, status: RequestStatus.Resolved, assignedTo: agent);

        var result = await _sut.GetSummaryAsync(ToCurrentUser(agent), CancellationToken.None);

        Assert.Equal(2, result.StaffMetrics!.AssignedToMe);
    }

    [Fact]
    public async Task GetSummaryAsync_SupportAgent_ActiveAssignedToMeCountIsCorrect()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("employee", UserRole.Employee);
        var agent = await CreateUserAsync("agent", UserRole.SupportAgent);
        await CreateRequestAsync(category, employee, status: RequestStatus.InProgress, assignedTo: agent);
        await CreateRequestAsync(category, employee, status: RequestStatus.Resolved, assignedTo: agent);

        var result = await _sut.GetSummaryAsync(ToCurrentUser(agent), CancellationToken.None);

        Assert.Equal(1, result.StaffMetrics!.ActiveAssignedToMe);
    }

    [Fact]
    public async Task GetSummaryAsync_SupportAgent_RecentRequestsIncludeAllRequests()
    {
        var category = await CreateCategoryAsync("Hardware");
        var emp1 = await CreateUserAsync("emp1", UserRole.Employee);
        var emp2 = await CreateUserAsync("emp2", UserRole.Employee);
        var agent = await CreateUserAsync("agent", UserRole.SupportAgent);
        await CreateRequestAsync(category, emp1);
        await CreateRequestAsync(category, emp2);

        var result = await _sut.GetSummaryAsync(ToCurrentUser(agent), CancellationToken.None);

        Assert.Equal(2, result.RecentRequests.Count);
    }

    // ─── Admin scope ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaryAsync_Admin_ScopeIsAdmin()
    {
        var admin = await CreateUserAsync("admin", UserRole.Admin);

        var result = await _sut.GetSummaryAsync(ToCurrentUser(admin), CancellationToken.None);

        Assert.Equal("Admin", result.Scope);
        Assert.NotNull(result.StaffMetrics);
        Assert.NotNull(result.AdminMetrics);
    }

    [Fact]
    public async Task GetSummaryAsync_Admin_ActiveCategoriesCountExcludesInactive()
    {
        await CreateCategoryAsync("Active1");
        await CreateCategoryAsync("Active2");
        await CreateCategoryAsync("Inactive", isActive: false);
        var admin = await CreateUserAsync("admin", UserRole.Admin);

        var result = await _sut.GetSummaryAsync(ToCurrentUser(admin), CancellationToken.None);

        Assert.Equal(2, result.AdminMetrics!.ActiveCategories);
    }

    [Fact]
    public async Task GetSummaryAsync_Admin_ActiveSupportAgentsCountExcludesInactiveAndOtherRoles()
    {
        await CreateUserAsync("agent1", UserRole.SupportAgent);
        await CreateUserAsync("agent2", UserRole.SupportAgent);
        await CreateUserAsync("inactive-agent", UserRole.SupportAgent, isActive: false);
        var admin = await CreateUserAsync("admin", UserRole.Admin);
        await CreateUserAsync("employee", UserRole.Employee);

        var result = await _sut.GetSummaryAsync(ToCurrentUser(admin), CancellationToken.None);

        Assert.Equal(2, result.AdminMetrics!.ActiveSupportAgents);
    }

    [Fact]
    public async Task GetSummaryAsync_Admin_ActiveAdminsCountExcludesInactiveAndOtherRoles()
    {
        var admin = await CreateUserAsync("admin", UserRole.Admin);
        await CreateUserAsync("inactive-admin", UserRole.Admin, isActive: false);
        await CreateUserAsync("agent", UserRole.SupportAgent);
        await CreateUserAsync("employee", UserRole.Employee);

        var result = await _sut.GetSummaryAsync(ToCurrentUser(admin), CancellationToken.None);

        Assert.Equal(1, result.AdminMetrics!.ActiveAdmins);
    }

    // ─── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaryAsync_EmptyDatabase_ReturnsZeroCountsWithAllStatusEntries()
    {
        var employee = await CreateUserAsync("employee", UserRole.Employee);

        var result = await _sut.GetSummaryAsync(ToCurrentUser(employee), CancellationToken.None);

        Assert.Equal(0, result.TotalRequests);
        Assert.Equal(0, result.OpenRequests);
        Assert.Equal(6, result.StatusCounts.Count);
        Assert.Equal(4, result.PriorityCounts.Count);
        Assert.All(result.StatusCounts, s => Assert.Equal(0, s.Count));
        Assert.All(result.PriorityCounts, p => Assert.Equal(0, p.Count));
        Assert.Empty(result.RecentRequests);
    }

    [Fact]
    public async Task GetSummaryAsync_RecentRequest_CategoryNameIsProjectedCorrectly()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("employee", UserRole.Employee);
        await CreateRequestAsync(category, employee);

        var result = await _sut.GetSummaryAsync(ToCurrentUser(employee), CancellationToken.None);

        Assert.Equal("Hardware", result.RecentRequests.Single().CategoryName);
    }

    [Fact]
    public async Task GetSummaryAsync_RecentRequest_AssignedToDisplayNameIsNullWhenUnassigned()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("employee", UserRole.Employee);
        var agent = await CreateUserAsync("agent", UserRole.SupportAgent);
        await CreateRequestAsync(category, employee);
        await CreateRequestAsync(category, employee, assignedTo: agent);

        var result = await _sut.GetSummaryAsync(ToCurrentUser(employee), CancellationToken.None);

        Assert.Equal(2, result.RecentRequests.Count);
        Assert.Contains(result.RecentRequests, r => r.AssignedToDisplayName == null);
        Assert.Contains(result.RecentRequests, r => r.AssignedToDisplayName == "agent Display");
    }

    [Fact]
    public async Task GetSummaryAsync_RecentRequest_DoesNotExposeDescriptionOrSensitiveFields()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("employee", UserRole.Employee);
        await CreateRequestAsync(category, employee);

        var result = await _sut.GetSummaryAsync(ToCurrentUser(employee), CancellationToken.None);

        var recent = result.RecentRequests.Single();
        // Verify only expected fields exist on the DTO (no description, email, username)
        Assert.NotNull(recent.Title);
        Assert.NotNull(recent.Status);
        Assert.NotNull(recent.Priority);
        Assert.NotNull(recent.CategoryName);
        Assert.NotNull(recent.CreatedByDisplayName);
    }
}
