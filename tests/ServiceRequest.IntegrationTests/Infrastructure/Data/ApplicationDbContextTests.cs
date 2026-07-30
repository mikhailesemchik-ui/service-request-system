using Microsoft.EntityFrameworkCore;
using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;
using ServiceRequest.Infrastructure.Data;

namespace ServiceRequest.IntegrationTests.Infrastructure.Data;

public sealed class ApplicationDbContextTests : IDisposable
{
    private readonly string _databasePath;
    private readonly string _connectionString;
    private readonly ApplicationDbContext _dbContext;

    public ApplicationDbContextTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"service-requests-tests-{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_databasePath};Pooling=False";

        _dbContext = CreateContext();
        _dbContext.Database.Migrate();
    }

    public void Dispose()
    {
        _dbContext.Dispose();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    [Fact]
    public async Task SaveAndLoad_WhenRequestHasCategoryAndCreator_PersistsAndReloadsRequest()
    {
        var category = new RequestCategory("Hardware");
        var creator = new ApplicationUser("jane.doe", "Jane Doe", "jane.doe@example.com", UserRole.Employee);
        _dbContext.RequestCategories.Add(category);
        _dbContext.Users.Add(creator);
        await _dbContext.SaveChangesAsync();

        var request = new SupportRequest(
            "Printer not working",
            "The office printer jams.",
            RequestPriority.High,
            category,
            creator);
        _dbContext.SupportRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        using var verifyContext = CreateContext();
        var loaded = await verifyContext.SupportRequests
            .Include(r => r.Category)
            .Include(r => r.CreatedByUser)
            .SingleAsync(r => r.Id == request.Id);

        Assert.Equal("Printer not working", loaded.Title);
        Assert.Equal("The office printer jams.", loaded.Description);
        Assert.Equal(category.Id, loaded.CategoryId);
        Assert.Equal(creator.Id, loaded.CreatedByUserId);
        Assert.Equal("Hardware", loaded.Category.Name);
        Assert.Equal("jane.doe", loaded.CreatedByUser.Username);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsCreatedAtAsUtcWithoutLoss()
    {
        var category = new RequestCategory("Hardware");
        var creator = new ApplicationUser("jane.doe", "Jane Doe", "jane.doe@example.com", UserRole.Employee);
        _dbContext.RequestCategories.Add(category);
        _dbContext.Users.Add(creator);
        await _dbContext.SaveChangesAsync();

        var request = new SupportRequest("Printer not working", "The office printer jams.", RequestPriority.High, category, creator);
        _dbContext.SupportRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        using var verifyContext = CreateContext();
        var loaded = await verifyContext.SupportRequests.SingleAsync(r => r.Id == request.Id);

        Assert.Equal(request.CreatedAt, loaded.CreatedAt);
        Assert.Equal(TimeSpan.Zero, loaded.CreatedAt.Offset);
    }

    [Fact]
    public async Task SaveAndLoad_WhenRequestHasNoAssignee_AllowsNullAssignee()
    {
        var category = new RequestCategory("Hardware");
        var creator = new ApplicationUser("jane.doe", "Jane Doe", "jane.doe@example.com", UserRole.Employee);
        _dbContext.RequestCategories.Add(category);
        _dbContext.Users.Add(creator);
        await _dbContext.SaveChangesAsync();

        var request = new SupportRequest(
            "Printer not working",
            "The office printer jams.",
            RequestPriority.Low,
            category,
            creator);
        _dbContext.SupportRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        using var verifyContext = CreateContext();
        var loaded = await verifyContext.SupportRequests.SingleAsync(r => r.Id == request.Id);

        Assert.Null(loaded.AssignedToUserId);
    }

    [Fact]
    public async Task SaveChanges_WhenUsernameIsDuplicate_ThrowsDbUpdateException()
    {
        _dbContext.Users.Add(new ApplicationUser("jane.doe", "Jane Doe", "jane.doe@example.com", UserRole.Employee));
        await _dbContext.SaveChangesAsync();

        _dbContext.Users.Add(new ApplicationUser("jane.doe", "Jane Other", "jane.other@example.com", UserRole.Employee));

        await Assert.ThrowsAsync<DbUpdateException>(() => _dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChanges_WhenCategoryNameIsDuplicate_ThrowsDbUpdateException()
    {
        _dbContext.RequestCategories.Add(new RequestCategory("Hardware"));
        await _dbContext.SaveChangesAsync();

        _dbContext.RequestCategories.Add(new RequestCategory("Hardware"));

        await Assert.ThrowsAsync<DbUpdateException>(() => _dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Delete_WhenRequestHasCommentsAndHistory_CascadesToCommentsAndHistory()
    {
        var category = new RequestCategory("Hardware");
        var creator = new ApplicationUser("jane.doe", "Jane Doe", "jane.doe@example.com", UserRole.Employee);
        _dbContext.RequestCategories.Add(category);
        _dbContext.Users.Add(creator);
        await _dbContext.SaveChangesAsync();

        var request = new SupportRequest(
            "Printer not working",
            "The office printer jams.",
            RequestPriority.Medium,
            category,
            creator);
        _dbContext.SupportRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        var comment = new RequestComment("Looking into it.", isInternal: false, request, creator);
        var history = new RequestHistory(request, creator, "Created", null, "New");
        _dbContext.RequestComments.Add(comment);
        _dbContext.RequestHistory.Add(history);
        await _dbContext.SaveChangesAsync();

        _dbContext.SupportRequests.Remove(request);
        await _dbContext.SaveChangesAsync();

        using var verifyContext = CreateContext();
        Assert.False(await verifyContext.RequestComments.AnyAsync(c => c.Id == comment.Id));
        Assert.False(await verifyContext.RequestHistory.AnyAsync(h => h.Id == history.Id));
    }

    [Fact]
    public async Task SaveChanges_WhenDeletingUserReferencedByRequest_ThrowsDbUpdateException()
    {
        var category = new RequestCategory("Hardware");
        var creator = new ApplicationUser("jane.doe", "Jane Doe", "jane.doe@example.com", UserRole.Employee);
        _dbContext.RequestCategories.Add(category);
        _dbContext.Users.Add(creator);
        await _dbContext.SaveChangesAsync();

        var request = new SupportRequest(
            "Printer not working",
            "The office printer jams.",
            RequestPriority.Medium,
            category,
            creator);
        _dbContext.SupportRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        using var deleteContext = CreateContext();
        var userToDelete = await deleteContext.Users.SingleAsync(u => u.Id == creator.Id);
        deleteContext.Users.Remove(userToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => deleteContext.SaveChangesAsync());
    }

    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }
}
