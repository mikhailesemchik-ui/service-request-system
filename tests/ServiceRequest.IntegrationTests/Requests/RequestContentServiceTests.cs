using Microsoft.EntityFrameworkCore;
using ServiceRequest.Application.Authentication;
using ServiceRequest.Application.Requests;
using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;
using ServiceRequest.Domain.Exceptions;
using ServiceRequest.Infrastructure.Data;
using ServiceRequest.Infrastructure.Requests;

namespace ServiceRequest.IntegrationTests.Requests;

public sealed class RequestContentServiceTests : IDisposable
{
    private readonly string _databasePath;
    private readonly string _connectionString;
    private readonly ApplicationDbContext _dbContext;
    private readonly RequestService _sut;

    public RequestContentServiceTests()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"service-requests-content-service-{Guid.NewGuid():N}.db");
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

    private async Task<RequestCategory> CreateCategoryAsync(string name = "Hardware")
    {
        var category = new RequestCategory(name);
        _dbContext.RequestCategories.Add(category);
        await _dbContext.SaveChangesAsync();
        return category;
    }

    private async Task<ApplicationUser> CreateUserAsync(
        string username,
        UserRole role,
        bool isActive = true)
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
        ApplicationUser? assignedTo = null,
        RequestStatus targetStatus = RequestStatus.New,
        string title = "Original title",
        string description = "Original description.")
    {
        var request = new SupportRequest(title, description, RequestPriority.Medium, category, creator);

        if (assignedTo is not null)
        {
            request.AssignTo(assignedTo);
        }

        _dbContext.SupportRequests.Add(request);
        await _dbContext.SaveChangesAsync();

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

    private static UpdateRequestContentRequest Command(
        string title = "Updated title",
        string description = "Updated description.") =>
        new() { Title = title, Description = description };

    private static AuthenticatedUserDto ToCurrentUser(ApplicationUser user, string? roleOverride = null) =>
        new(user.Id, user.Username, user.DisplayName, user.Email, roleOverride ?? user.Role.ToString());

    [Fact]
    public async Task UpdateContentAsync_WhenEmployeeOwnsNewRequest_Succeeds()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var request = await CreateRequestAsync(category, employee);

        var result = await _sut.UpdateContentAsync(request.Id, Command(), ToCurrentUser(employee), CancellationToken.None);

        Assert.Equal("Updated title", result.Title);
        Assert.Equal("Updated description.", result.Description);
    }

    [Fact]
    public async Task UpdateContentAsync_WhenEmployeeDoesNotOwnRequest_ThrowsSupportRequestNotFoundException()
    {
        var category = await CreateCategoryAsync();
        var owner = await CreateUserAsync("owner", UserRole.Employee);
        var other = await CreateUserAsync("other", UserRole.Employee);
        var request = await CreateRequestAsync(category, owner);

        await Assert.ThrowsAsync<SupportRequestNotFoundException>(() =>
            _sut.UpdateContentAsync(request.Id, Command(), ToCurrentUser(other), CancellationToken.None));
    }

    [Theory]
    [InlineData(RequestStatus.InProgress)]
    [InlineData(RequestStatus.WaitingForUser)]
    [InlineData(RequestStatus.Resolved)]
    [InlineData(RequestStatus.Closed)]
    [InlineData(RequestStatus.Cancelled)]
    public async Task UpdateContentAsync_WhenEmployeeOwnsNonNewRequest_ThrowsRequestContentLockedException(RequestStatus status)
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee, assignedTo: agent, targetStatus: status);

        await Assert.ThrowsAsync<RequestContentLockedException>(() =>
            _sut.UpdateContentAsync(request.Id, Command(), ToCurrentUser(employee), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateContentAsync_WhenSupportAgentAssignedNonTerminalRequest_Succeeds()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee, assignedTo: agent, targetStatus: RequestStatus.InProgress);

        var result = await _sut.UpdateContentAsync(request.Id, Command(), ToCurrentUser(agent), CancellationToken.None);

        Assert.Equal("Updated title", result.Title);
    }

    [Fact]
    public async Task UpdateContentAsync_WhenSupportAgentRequestIsUnassigned_ThrowsRequestContentForbiddenException()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);

        await Assert.ThrowsAsync<RequestContentForbiddenException>(() =>
            _sut.UpdateContentAsync(request.Id, Command(), ToCurrentUser(agent), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateContentAsync_WhenSupportAgentRequestIsAssignedToAnotherAgent_ThrowsRequestContentForbiddenException()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var firstAgent = await CreateUserAsync("agent.one", UserRole.SupportAgent);
        var secondAgent = await CreateUserAsync("agent.two", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee, assignedTo: firstAgent, targetStatus: RequestStatus.InProgress);

        await Assert.ThrowsAsync<RequestContentForbiddenException>(() =>
            _sut.UpdateContentAsync(request.Id, Command(), ToCurrentUser(secondAgent), CancellationToken.None));
    }

    [Theory]
    [InlineData(RequestStatus.Closed)]
    [InlineData(RequestStatus.Cancelled)]
    public async Task UpdateContentAsync_WhenSupportAgentAssignedTerminalRequest_ThrowsRequestContentLockedException(RequestStatus status)
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee, assignedTo: agent, targetStatus: status);

        await Assert.ThrowsAsync<RequestContentLockedException>(() =>
            _sut.UpdateContentAsync(request.Id, Command(), ToCurrentUser(agent), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateContentAsync_WhenAdminEditsNonTerminalRequest_Succeeds()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee, targetStatus: RequestStatus.New);

        var result = await _sut.UpdateContentAsync(request.Id, Command(), ToCurrentUser(admin), CancellationToken.None);

        Assert.Equal("Updated description.", result.Description);
    }

    [Theory]
    [InlineData(RequestStatus.Closed)]
    [InlineData(RequestStatus.Cancelled)]
    public async Task UpdateContentAsync_WhenAdminEditsTerminalRequest_ThrowsRequestContentLockedException(RequestStatus status)
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee, assignedTo: agent, targetStatus: status);

        await Assert.ThrowsAsync<RequestContentLockedException>(() =>
            _sut.UpdateContentAsync(request.Id, Command(), ToCurrentUser(admin), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateContentAsync_WhenActorIsMissing_ThrowsCurrentUserUnavailableException()
    {
        await Assert.ThrowsAsync<CurrentUserUnavailableException>(() =>
            _sut.UpdateContentAsync(1, Command(), new AuthenticatedUserDto(999, "missing", "Missing", "missing@example.test", "Admin"), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateContentAsync_WhenActorIsInactive_ThrowsCurrentUserUnavailableException()
    {
        var inactive = await CreateUserAsync("inactive", UserRole.Admin, isActive: false);

        await Assert.ThrowsAsync<CurrentUserUnavailableException>(() =>
            _sut.UpdateContentAsync(1, Command(), ToCurrentUser(inactive), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateContentAsync_UsesDatabaseRoleInsteadOfIncomingAuthenticatedUserRole()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee, assignedTo: agent, targetStatus: RequestStatus.InProgress);

        var result = await _sut.UpdateContentAsync(
            request.Id,
            Command(),
            ToCurrentUser(agent, roleOverride: nameof(UserRole.Employee)),
            CancellationToken.None);

        Assert.Equal("Updated title", result.Title);
    }

    [Fact]
    public async Task UpdateContentAsync_WhenTitleOnlyChanges_AddsOneTitleChangedHistoryEntry()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee);

        await _sut.UpdateContentAsync(request.Id, Command(title: "New title", description: request.Description), ToCurrentUser(admin), CancellationToken.None);

        var history = await _dbContext.RequestHistory.SingleAsync();
        Assert.Equal(RequestHistoryActions.TitleChanged, history.Action);
        Assert.Equal("Original title", history.PreviousValue);
        Assert.Equal("New title", history.NewValue);
    }

    [Fact]
    public async Task UpdateContentAsync_WhenDescriptionOnlyChanges_AddsOneDescriptionChangedHistoryEntry()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee);

        await _sut.UpdateContentAsync(request.Id, Command(title: request.Title, description: "New description."), ToCurrentUser(admin), CancellationToken.None);

        var history = await _dbContext.RequestHistory.SingleAsync();
        Assert.Equal(RequestHistoryActions.DescriptionChanged, history.Action);
        Assert.Equal("Original description.", history.PreviousValue);
        Assert.Equal("New description.", history.NewValue);
    }

    [Fact]
    public async Task UpdateContentAsync_WhenBothFieldsChange_AddsExactlyTwoHistoryEntries()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee);

        await _sut.UpdateContentAsync(request.Id, Command(title: "New title", description: "New description."), ToCurrentUser(admin), CancellationToken.None);

        var history = await _dbContext.RequestHistory.OrderBy(h => h.Id).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal(RequestHistoryActions.TitleChanged, history[0].Action);
        Assert.Equal(RequestHistoryActions.DescriptionChanged, history[1].Action);
    }

    [Fact]
    public async Task UpdateContentAsync_WhenNoOp_AddsNoHistoryEntries()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee);
        var updatedAtBefore = request.UpdatedAt;

        var result = await _sut.UpdateContentAsync(
            request.Id,
            Command(title: $"  {request.Title}  ", description: $"  {request.Description}  "),
            ToCurrentUser(admin),
            CancellationToken.None);

        var historyCount = await _dbContext.RequestHistory.CountAsync();
        Assert.Equal(0, historyCount);
        Assert.Equal(updatedAtBefore, result.UpdatedAt);
    }

    [Fact]
    public async Task UpdateContentAsync_WhenRepeatedIdenticalUpdate_AddsNoExtraHistoryEntries()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee);

        await _sut.UpdateContentAsync(request.Id, Command(), ToCurrentUser(admin), CancellationToken.None);
        await _sut.UpdateContentAsync(request.Id, Command(), ToCurrentUser(admin), CancellationToken.None);

        var historyCount = await _dbContext.RequestHistory.CountAsync();
        Assert.Equal(2, historyCount);
    }

    [Fact]
    public async Task UpdateContentAsync_StoresCurrentDatabaseActorInHistory()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee);

        await _sut.UpdateContentAsync(request.Id, Command(title: "New title", description: request.Description), ToCurrentUser(admin), CancellationToken.None);

        var history = await _dbContext.RequestHistory.SingleAsync();
        Assert.Equal(admin.Id, history.ChangedByUserId);
    }

    [Fact]
    public async Task UpdateContentAsync_ReturnsNormalizedResponseValues()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee);

        var result = await _sut.UpdateContentAsync(
            request.Id,
            Command(title: "  Normalized title  ", description: "  Normalized description.  "),
            ToCurrentUser(admin),
            CancellationToken.None);

        Assert.Equal("Normalized title", result.Title);
        Assert.Equal("Normalized description.", result.Description);
    }

    [Fact]
    public async Task UpdateContentAsync_CollapsesWhitespaceInDescriptionSummary()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee, description: "Old\n\n description");

        await _sut.UpdateContentAsync(request.Id, Command(title: request.Title, description: "New\r\n\t description"), ToCurrentUser(admin), CancellationToken.None);

        var history = await _dbContext.RequestHistory.SingleAsync();
        Assert.Equal("Old description", history.PreviousValue);
        Assert.Equal("New description", history.NewValue);
    }

    [Fact]
    public async Task UpdateContentAsync_WhenDescriptionSummaryIsExactly120Characters_DoesNotTruncate()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee);
        var description = new string('a', 120);

        await _sut.UpdateContentAsync(request.Id, Command(title: request.Title, description: description), ToCurrentUser(admin), CancellationToken.None);

        var history = await _dbContext.RequestHistory.SingleAsync();
        Assert.Equal(120, history.NewValue!.Length);
        Assert.Equal(description, history.NewValue);
    }

    [Fact]
    public async Task UpdateContentAsync_WhenDescriptionSummaryIsOver120Characters_TruncatesTo120WithEllipsis()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee);
        var description = new string('a', 121);

        await _sut.UpdateContentAsync(request.Id, Command(title: request.Title, description: description), ToCurrentUser(admin), CancellationToken.None);

        var history = await _dbContext.RequestHistory.SingleAsync();
        Assert.Equal(120, history.NewValue!.Length);
        Assert.EndsWith("...", history.NewValue);
        Assert.DoesNotContain(description, history.NewValue);
    }

    [Fact]
    public async Task UpdateContentAsync_WhenLongDescriptionChanges_DoesNotStoreFullDescriptionInHistoryValues()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var admin = await CreateUserAsync("root.admin", UserRole.Admin);
        var request = await CreateRequestAsync(category, employee, description: new string('b', 130));
        var longDescription = new string('a', 130);

        await _sut.UpdateContentAsync(request.Id, Command(title: request.Title, description: longDescription), ToCurrentUser(admin), CancellationToken.None);

        var history = await _dbContext.RequestHistory.SingleAsync();
        Assert.NotEqual(new string('b', 130), history.PreviousValue);
        Assert.NotEqual(longDescription, history.NewValue);
        Assert.True(history.PreviousValue!.Length <= 120);
        Assert.True(history.NewValue!.Length <= 120);
    }

    [Fact]
    public async Task UpdateContentAsync_WhenRejected_DoesNotChangeRequestOrHistory()
    {
        var category = await CreateCategoryAsync();
        var owner = await CreateUserAsync("owner", UserRole.Employee);
        var other = await CreateUserAsync("other", UserRole.Employee);
        var request = await CreateRequestAsync(category, owner);

        await Assert.ThrowsAsync<SupportRequestNotFoundException>(() =>
            _sut.UpdateContentAsync(request.Id, Command(), ToCurrentUser(other), CancellationToken.None));

        var persisted = await _dbContext.SupportRequests.SingleAsync(r => r.Id == request.Id);
        var historyCount = await _dbContext.RequestHistory.CountAsync();
        Assert.Equal("Original title", persisted.Title);
        Assert.Equal("Original description.", persisted.Description);
        Assert.Equal(0, historyCount);
    }
}
