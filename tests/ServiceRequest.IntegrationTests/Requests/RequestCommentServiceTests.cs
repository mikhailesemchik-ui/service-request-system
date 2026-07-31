using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ServiceRequest.Application.Authentication;
using ServiceRequest.Application.Requests;
using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;
using ServiceRequest.Domain.Exceptions;
using ServiceRequest.Infrastructure.Data;
using ServiceRequest.Infrastructure.Requests;

namespace ServiceRequest.IntegrationTests.Requests;

public sealed class RequestCommentServiceTests : IDisposable
{
    private readonly string _databasePath;
    private readonly string _connectionString;
    private readonly ApplicationDbContext _dbContext;
    private readonly RequestService _sut;

    public RequestCommentServiceTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"service-requests-comment-tests-{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_databasePath};Pooling=False";

        _dbContext = CreateContext();
        _dbContext.Database.Migrate();
        _sut = new RequestService(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        SqliteConnection.ClearAllPools();

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

    private async Task<RequestCategory> CreateCategoryAsync()
    {
        var category = new RequestCategory("Hardware");
        _dbContext.RequestCategories.Add(category);
        await _dbContext.SaveChangesAsync();
        return category;
    }

    private async Task<ApplicationUser> CreateUserAsync(string username, UserRole role)
    {
        var user = new ApplicationUser(username, $"{username} Display", $"{username}@example.test", role);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    private async Task<SupportRequest> CreateRequestAsync(RequestCategory category, ApplicationUser creator)
    {
        var request = new SupportRequest("Printer not working", "The office printer jams.", RequestPriority.Medium, category, creator);
        _dbContext.SupportRequests.Add(request);
        await _dbContext.SaveChangesAsync();
        return request;
    }

    private static AuthenticatedUserDto ToCurrentUser(ApplicationUser user) =>
        new(user.Id, user.Username, user.DisplayName, user.Email, user.Role.ToString());

    // GetCommentsAsync

    [Fact]
    public async Task GetCommentsAsync_WhenEmployeeRequestsOwnRequest_ReturnsPublicCommentsOnly()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);

        _dbContext.RequestComments.Add(new RequestComment("Public comment", false, request, agent));
        _dbContext.RequestComments.Add(new RequestComment("Internal note", true, request, agent));
        await _dbContext.SaveChangesAsync();

        var comments = await _sut.GetCommentsAsync(request.Id, ToCurrentUser(employee), CancellationToken.None);

        Assert.Single(comments);
        Assert.Equal("Public comment", comments[0].Content);
        Assert.False(comments[0].IsInternal);
    }

    [Fact]
    public async Task GetCommentsAsync_WhenEmployeeRequestsAnotherUsersRequest_ThrowsNotFoundException()
    {
        var category = await CreateCategoryAsync();
        var owner = await CreateUserAsync("jane.doe", UserRole.Employee);
        var employee = await CreateUserAsync("other.user", UserRole.Employee);
        var request = await CreateRequestAsync(category, owner);

        await Assert.ThrowsAsync<SupportRequestNotFoundException>(
            () => _sut.GetCommentsAsync(request.Id, ToCurrentUser(employee), CancellationToken.None));
    }

    [Fact]
    public async Task GetCommentsAsync_WhenStaffRequests_ReturnsBothPublicAndInternalComments()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);

        _dbContext.RequestComments.Add(new RequestComment("Public comment", false, request, employee));
        _dbContext.RequestComments.Add(new RequestComment("Internal note", true, request, agent));
        await _dbContext.SaveChangesAsync();

        var comments = await _sut.GetCommentsAsync(request.Id, ToCurrentUser(agent), CancellationToken.None);

        Assert.Equal(2, comments.Count);
    }

    [Fact]
    public async Task GetCommentsAsync_WhenRequestDoesNotExist_ThrowsNotFoundException()
    {
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);

        await Assert.ThrowsAsync<SupportRequestNotFoundException>(
            () => _sut.GetCommentsAsync(99999, ToCurrentUser(agent), CancellationToken.None));
    }

    [Fact]
    public async Task GetCommentsAsync_WhenEmpty_ReturnsEmptyList()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var request = await CreateRequestAsync(category, employee);

        var comments = await _sut.GetCommentsAsync(request.Id, ToCurrentUser(employee), CancellationToken.None);

        Assert.Empty(comments);
    }

    [Fact]
    public async Task GetCommentsAsync_ReturnsCommentsOrderedByCreatedAt()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var request = await CreateRequestAsync(category, employee);

        _dbContext.RequestComments.Add(new RequestComment("First comment", false, request, employee));
        await _dbContext.SaveChangesAsync();
        await Task.Delay(10); // ensure distinct timestamps
        _dbContext.RequestComments.Add(new RequestComment("Second comment", false, request, employee));
        await _dbContext.SaveChangesAsync();

        var comments = await _sut.GetCommentsAsync(request.Id, ToCurrentUser(employee), CancellationToken.None);

        Assert.Equal(2, comments.Count);
        Assert.Equal("First comment", comments[0].Content);
        Assert.Equal("Second comment", comments[1].Content);
    }

    // AddCommentAsync

    [Fact]
    public async Task AddCommentAsync_WhenEmployeeAddsPublicCommentToOwnRequest_ReturnsCreatedComment()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var request = await CreateRequestAsync(category, employee);

        var comment = await _sut.AddCommentAsync(
            request.Id,
            new CreateCommentRequest { Content = "My comment", IsInternal = false },
            ToCurrentUser(employee),
            CancellationToken.None);

        Assert.Equal("My comment", comment.Content);
        Assert.False(comment.IsInternal);
        Assert.Equal(employee.Id, comment.Author.Id);
    }

    [Fact]
    public async Task AddCommentAsync_WhenEmployeeAddsToAnotherUsersRequest_ThrowsNotFoundException()
    {
        var category = await CreateCategoryAsync();
        var owner = await CreateUserAsync("jane.doe", UserRole.Employee);
        var employee = await CreateUserAsync("other.user", UserRole.Employee);
        var request = await CreateRequestAsync(category, owner);

        await Assert.ThrowsAsync<SupportRequestNotFoundException>(
            () => _sut.AddCommentAsync(
                request.Id,
                new CreateCommentRequest { Content = "My comment", IsInternal = false },
                ToCurrentUser(employee),
                CancellationToken.None));
    }

    [Fact]
    public async Task AddCommentAsync_WhenEmployeeAddsInternalComment_ThrowsForbiddenException()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var request = await CreateRequestAsync(category, employee);

        await Assert.ThrowsAsync<InternalCommentForbiddenException>(
            () => _sut.AddCommentAsync(
                request.Id,
                new CreateCommentRequest { Content = "Should be forbidden", IsInternal = true },
                ToCurrentUser(employee),
                CancellationToken.None));
    }

    [Theory]
    [InlineData(RequestStatus.Closed)]
    [InlineData(RequestStatus.Cancelled)]
    public async Task AddCommentAsync_WhenRequestIsTerminal_ThrowsClosedException(RequestStatus terminalStatus)
    {
        var category = await CreateCategoryAsync();
        var admin = await CreateUserAsync("admin.user", UserRole.Admin);
        var request = await CreateRequestAsync(category, admin);

        if (terminalStatus == RequestStatus.Closed)
        {
            request.AssignTo(admin);
            request.ChangeStatus(RequestStatus.InProgress);
            request.ChangeStatus(RequestStatus.Resolved);
            request.ChangeStatus(RequestStatus.Closed);
        }
        else
        {
            request.ChangeStatus(RequestStatus.Cancelled);
        }

        await _dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<RequestCommentsClosedException>(
            () => _sut.AddCommentAsync(
                request.Id,
                new CreateCommentRequest { Content = "Too late", IsInternal = false },
                ToCurrentUser(admin),
                CancellationToken.None));
    }

    [Fact]
    public async Task AddCommentAsync_WhenStaffAddsInternalComment_Succeeds()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);

        var comment = await _sut.AddCommentAsync(
            request.Id,
            new CreateCommentRequest { Content = "Staff note", IsInternal = true },
            ToCurrentUser(agent),
            CancellationToken.None);

        Assert.True(comment.IsInternal);
        Assert.Equal("Staff note", comment.Content);
        Assert.Equal(agent.Id, comment.Author.Id);
    }

    [Fact]
    public async Task AddCommentAsync_WhenStaffAddsCommentToAnyRequest_Succeeds()
    {
        var category = await CreateCategoryAsync();
        var employee = await CreateUserAsync("jane.doe", UserRole.Employee);
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);
        var request = await CreateRequestAsync(category, employee);

        var comment = await _sut.AddCommentAsync(
            request.Id,
            new CreateCommentRequest { Content = "Looking into it.", IsInternal = false },
            ToCurrentUser(agent),
            CancellationToken.None);

        Assert.Equal("Looking into it.", comment.Content);
    }

    [Fact]
    public async Task AddCommentAsync_WhenRequestDoesNotExist_ThrowsNotFoundException()
    {
        var agent = await CreateUserAsync("agent.smith", UserRole.SupportAgent);

        await Assert.ThrowsAsync<SupportRequestNotFoundException>(
            () => _sut.AddCommentAsync(
                99999,
                new CreateCommentRequest { Content = "No such request", IsInternal = false },
                ToCurrentUser(agent),
                CancellationToken.None));
    }
}
