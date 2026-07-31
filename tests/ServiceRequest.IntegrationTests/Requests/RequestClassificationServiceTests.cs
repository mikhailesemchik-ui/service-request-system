using Microsoft.EntityFrameworkCore;
using ServiceRequest.Application.Authentication;
using ServiceRequest.Application.Requests;
using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;
using ServiceRequest.Domain.Exceptions;
using ServiceRequest.Infrastructure.Data;
using ServiceRequest.Infrastructure.Requests;

namespace ServiceRequest.IntegrationTests.Requests;

public sealed class RequestClassificationServiceTests : IDisposable
{
    private readonly string _databasePath;
    private readonly string _connectionString;
    private readonly ApplicationDbContext _dbContext;
    private readonly RequestService _sut;

    public RequestClassificationServiceTests()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"service-requests-classification-service-{Guid.NewGuid():N}.db");
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
        RequestPriority priority = RequestPriority.Medium,
        ApplicationUser? assignedTo = null)
    {
        var request = new SupportRequest("Printer not working", "The printer jams.", priority, category, creator);

        if (assignedTo is not null)
        {
            request.AssignTo(assignedTo);
        }

        _dbContext.SupportRequests.Add(request);
        await _dbContext.SaveChangesAsync();
        return request;
    }

    private async Task<SupportRequest> CreateRequestInStatusAsync(
        RequestCategory category,
        ApplicationUser creator,
        RequestStatus targetStatus,
        ApplicationUser? agent = null)
    {
        var request = await CreateRequestAsync(category, creator, assignedTo: agent);

        if (targetStatus == RequestStatus.New)
        {
            return request;
        }

        if (targetStatus == RequestStatus.Cancelled)
        {
            request.ChangeStatus(RequestStatus.Cancelled);
            await _dbContext.SaveChangesAsync();
            return request;
        }

        request.ChangeStatus(RequestStatus.InProgress);

        if (targetStatus == RequestStatus.InProgress)
        {
            await _dbContext.SaveChangesAsync();
            return request;
        }

        if (targetStatus == RequestStatus.WaitingForUser)
        {
            request.ChangeStatus(RequestStatus.WaitingForUser);
            await _dbContext.SaveChangesAsync();
            return request;
        }

        request.ChangeStatus(RequestStatus.Resolved);

        if (targetStatus == RequestStatus.Resolved)
        {
            await _dbContext.SaveChangesAsync();
            return request;
        }

        request.ChangeStatus(RequestStatus.Closed);
        await _dbContext.SaveChangesAsync();
        return request;
    }

    private static AuthenticatedUserDto ToCurrentUser(ApplicationUser user) =>
        new(user.Id, user.Username, user.DisplayName, user.Email, user.Role.ToString());

    // Permissions

    [Fact]
    public async Task UpdateClassificationAsync_WhenEmployee_ThrowsRequestClassificationForbiddenException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var newCategory = await CreateCategoryAsync("Software");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var request = await CreateRequestAsync(category, employee);

        var command = new UpdateRequestClassificationRequest { CategoryId = newCategory.Id, Priority = RequestPriority.High };

        await Assert.ThrowsAsync<RequestClassificationForbiddenException>(() =>
            _sut.UpdateClassificationAsync(request.Id, command, ToCurrentUser(employee), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateClassificationAsync_WhenSupportAgentNotAssigned_ThrowsRequestClassificationForbiddenException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var newCategory = await CreateCategoryAsync("Software");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);

        var command = new UpdateRequestClassificationRequest { CategoryId = newCategory.Id, Priority = RequestPriority.High };

        await Assert.ThrowsAsync<RequestClassificationForbiddenException>(() =>
            _sut.UpdateClassificationAsync(request.Id, command, ToCurrentUser(agent), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateClassificationAsync_WhenSupportAgentAssignedToOther_ThrowsRequestClassificationForbiddenException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var newCategory = await CreateCategoryAsync("Software");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent1 = await CreateUserAsync("agent.one", UserRole.SupportAgent);
        var agent2 = await CreateUserAsync("agent.two", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee, assignedTo: agent1);

        var command = new UpdateRequestClassificationRequest { CategoryId = newCategory.Id, Priority = RequestPriority.High };

        await Assert.ThrowsAsync<RequestClassificationForbiddenException>(() =>
            _sut.UpdateClassificationAsync(request.Id, command, ToCurrentUser(agent2), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateClassificationAsync_WhenSupportAgentAssigned_Succeeds()
    {
        var category = await CreateCategoryAsync("Hardware");
        var newCategory = await CreateCategoryAsync("Software");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee, assignedTo: agent);

        var command = new UpdateRequestClassificationRequest { CategoryId = newCategory.Id, Priority = RequestPriority.High };

        var result = await _sut.UpdateClassificationAsync(request.Id, command, ToCurrentUser(agent), CancellationToken.None);

        Assert.Equal(newCategory.Id, result.Category.Id);
        Assert.Equal(RequestPriority.High, result.Priority);
    }

    [Fact]
    public async Task UpdateClassificationAsync_WhenAdmin_Succeeds()
    {
        var category = await CreateCategoryAsync("Hardware");
        var newCategory = await CreateCategoryAsync("Software");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee);

        var command = new UpdateRequestClassificationRequest { CategoryId = newCategory.Id, Priority = RequestPriority.High };

        var result = await _sut.UpdateClassificationAsync(request.Id, command, ToCurrentUser(admin), CancellationToken.None);

        Assert.Equal(newCategory.Id, result.Category.Id);
        Assert.Equal(RequestPriority.High, result.Priority);
    }

    // Terminal status rejection

    [Fact]
    public async Task UpdateClassificationAsync_WhenClosed_ThrowsRequestClassificationLockedException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var newCategory = await CreateCategoryAsync("Software");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestInStatusAsync(category, employee, RequestStatus.Closed, agent);

        var command = new UpdateRequestClassificationRequest { CategoryId = newCategory.Id, Priority = RequestPriority.High };

        await Assert.ThrowsAsync<RequestClassificationLockedException>(() =>
            _sut.UpdateClassificationAsync(request.Id, command, ToCurrentUser(admin), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateClassificationAsync_WhenCancelled_ThrowsRequestClassificationLockedException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var newCategory = await CreateCategoryAsync("Software");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestInStatusAsync(category, employee, RequestStatus.Cancelled);

        var command = new UpdateRequestClassificationRequest { CategoryId = newCategory.Id, Priority = RequestPriority.High };

        await Assert.ThrowsAsync<RequestClassificationLockedException>(() =>
            _sut.UpdateClassificationAsync(request.Id, command, ToCurrentUser(admin), CancellationToken.None));
    }

    // Category validation

    [Fact]
    public async Task UpdateClassificationAsync_WhenCategoryMissing_ThrowsRequestCategoryNotFoundException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee);

        var command = new UpdateRequestClassificationRequest { CategoryId = 99999, Priority = RequestPriority.Medium };

        await Assert.ThrowsAsync<RequestCategoryNotFoundException>(() =>
            _sut.UpdateClassificationAsync(request.Id, command, ToCurrentUser(admin), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateClassificationAsync_WhenCategoryInactive_ThrowsRequestCategoryInactiveException()
    {
        var category = await CreateCategoryAsync("Hardware");
        var inactiveCategory = await CreateCategoryAsync("Archived", isActive: false);
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee);

        var command = new UpdateRequestClassificationRequest { CategoryId = inactiveCategory.Id, Priority = RequestPriority.Medium };

        await Assert.ThrowsAsync<RequestCategoryInactiveException>(() =>
            _sut.UpdateClassificationAsync(request.Id, command, ToCurrentUser(admin), CancellationToken.None));
    }

    // Idempotency

    [Fact]
    public async Task UpdateClassificationAsync_WhenCategoryAndPriorityUnchanged_SucceedsWithNoHistoryEntries()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee, priority: RequestPriority.Medium);

        var command = new UpdateRequestClassificationRequest { CategoryId = category.Id, Priority = RequestPriority.Medium };

        var result = await _sut.UpdateClassificationAsync(request.Id, command, ToCurrentUser(admin), CancellationToken.None);

        var historyCount = await _dbContext.RequestHistory.CountAsync();

        Assert.Equal(category.Id, result.Category.Id);
        Assert.Equal(RequestPriority.Medium, result.Priority);
        Assert.Equal(0, historyCount);
    }

    // History recording

    [Fact]
    public async Task UpdateClassificationAsync_WhenCategoryChanges_AddsOneCategoryChangedHistoryEntry()
    {
        var category = await CreateCategoryAsync("Hardware");
        var newCategory = await CreateCategoryAsync("Software");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee, priority: RequestPriority.Medium);

        var command = new UpdateRequestClassificationRequest { CategoryId = newCategory.Id, Priority = RequestPriority.Medium };

        await _sut.UpdateClassificationAsync(request.Id, command, ToCurrentUser(admin), CancellationToken.None);

        var history = await _dbContext.RequestHistory.ToListAsync();

        Assert.Single(history);
        Assert.Equal(RequestHistoryActions.CategoryChanged, history[0].Action);
        Assert.Equal(category.Id.ToString(), history[0].PreviousValue);
        Assert.Equal(newCategory.Id.ToString(), history[0].NewValue);
    }

    [Fact]
    public async Task UpdateClassificationAsync_WhenPriorityChanges_AddsOnePriorityChangedHistoryEntry()
    {
        var category = await CreateCategoryAsync("Hardware");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee, priority: RequestPriority.Low);

        var command = new UpdateRequestClassificationRequest { CategoryId = category.Id, Priority = RequestPriority.High };

        await _sut.UpdateClassificationAsync(request.Id, command, ToCurrentUser(admin), CancellationToken.None);

        var history = await _dbContext.RequestHistory.ToListAsync();

        Assert.Single(history);
        Assert.Equal(RequestHistoryActions.PriorityChanged, history[0].Action);
        Assert.Equal(RequestPriority.Low.ToString(), history[0].PreviousValue);
        Assert.Equal(RequestPriority.High.ToString(), history[0].NewValue);
    }

    [Fact]
    public async Task UpdateClassificationAsync_WhenBothChange_AddsTwoHistoryEntries()
    {
        var category = await CreateCategoryAsync("Hardware");
        var newCategory = await CreateCategoryAsync("Software");
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee, priority: RequestPriority.Low);

        var command = new UpdateRequestClassificationRequest { CategoryId = newCategory.Id, Priority = RequestPriority.High };

        await _sut.UpdateClassificationAsync(request.Id, command, ToCurrentUser(admin), CancellationToken.None);

        var history = await _dbContext.RequestHistory
            .OrderBy(h => h.Action)
            .ToListAsync();

        Assert.Equal(2, history.Count);
        Assert.Contains(history, h => h.Action == RequestHistoryActions.CategoryChanged);
        Assert.Contains(history, h => h.Action == RequestHistoryActions.PriorityChanged);
    }

    // Missing request

    [Fact]
    public async Task UpdateClassificationAsync_WhenRequestMissing_ThrowsSupportRequestNotFoundException()
    {
        var newCategory = await CreateCategoryAsync("Software");
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);

        var command = new UpdateRequestClassificationRequest { CategoryId = newCategory.Id, Priority = RequestPriority.Medium };

        await Assert.ThrowsAsync<SupportRequestNotFoundException>(() =>
            _sut.UpdateClassificationAsync(99999, command, ToCurrentUser(admin), CancellationToken.None));
    }
}
