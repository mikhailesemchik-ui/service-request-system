using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;

namespace ServiceRequest.UnitTests.Domain.Entities;

public class SupportRequestTests
{
    private static RequestCategory CreateCategory() => new("Hardware", "Hardware-related requests");

    private static ApplicationUser CreateUser() =>
        new("jane.doe", "Jane Doe", "jane.doe@example.com", UserRole.Employee);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenTitleIsBlank_ThrowsArgumentException(string title)
    {
        Assert.Throws<ArgumentException>(() =>
            new SupportRequest(title, "Description", RequestPriority.Medium, CreateCategory(), CreateUser()));
    }

    [Fact]
    public void Constructor_WhenTitleIsNull_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new SupportRequest(null!, "Description", RequestPriority.Medium, CreateCategory(), CreateUser()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenDescriptionIsBlank_ThrowsArgumentException(string description)
    {
        Assert.Throws<ArgumentException>(() =>
            new SupportRequest("Title", description, RequestPriority.Medium, CreateCategory(), CreateUser()));
    }

    [Fact]
    public void Constructor_WhenDescriptionIsNull_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new SupportRequest("Title", null!, RequestPriority.Medium, CreateCategory(), CreateUser()));
    }

    [Fact]
    public void Constructor_WhenCategoryIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SupportRequest("Title", "Description", RequestPriority.Medium, null!, CreateUser()));
    }

    [Fact]
    public void Constructor_WhenCreatedByUserIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SupportRequest("Title", "Description", RequestPriority.Medium, CreateCategory(), null!));
    }

    [Fact]
    public void Constructor_WhenValuesAreValid_RetainsSuppliedValues()
    {
        var category = CreateCategory();
        var creator = CreateUser();

        var request = new SupportRequest("Printer not working", "The office printer jams.", RequestPriority.High, category, creator);

        Assert.Equal("Printer not working", request.Title);
        Assert.Equal("The office printer jams.", request.Description);
        Assert.Equal(RequestPriority.High, request.Priority);
        Assert.Equal(RequestStatus.New, request.Status);
        Assert.Same(category, request.Category);
        Assert.Same(creator, request.CreatedByUser);
        Assert.Null(request.AssignedToUser);
    }

    [Fact]
    public void Constructor_TrimsTitle()
    {
        var request = new SupportRequest("  Printer not working  ", "The office printer jams.", RequestPriority.High, CreateCategory(), CreateUser());

        Assert.Equal("Printer not working", request.Title);
    }

    [Fact]
    public void Constructor_TrimsDescription()
    {
        var request = new SupportRequest("Printer not working", "  The office printer jams.  ", RequestPriority.High, CreateCategory(), CreateUser());

        Assert.Equal("The office printer jams.", request.Description);
    }

    [Fact]
    public void Constructor_NewRequest_HasNoAssignee()
    {
        var request = new SupportRequest("Printer not working", "The office printer jams.", RequestPriority.High, CreateCategory(), CreateUser());

        Assert.Null(request.AssignedToUser);
        Assert.Null(request.AssignedToUserId);
    }

    [Fact]
    public void Constructor_NewRequest_HasNullLifecycleTimestamps()
    {
        var request = new SupportRequest("Printer not working", "The office printer jams.", RequestPriority.High, CreateCategory(), CreateUser());

        Assert.Null(request.ResolvedAt);
        Assert.Null(request.ClosedAt);
        Assert.Null(request.CancelledAt);
    }

    [Fact]
    public void Constructor_NewRequest_SetsCreatedAtAndUpdatedAtToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;

        var request = new SupportRequest("Printer not working", "The office printer jams.", RequestPriority.High, CreateCategory(), CreateUser());

        var after = DateTimeOffset.UtcNow;

        Assert.InRange(request.CreatedAt, before, after);
        Assert.Equal(request.CreatedAt, request.UpdatedAt);
    }
}
