using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;

namespace ServiceRequest.UnitTests.Domain.Entities;

public class RequestCommentTests
{
    private static ApplicationUser CreateUser() =>
        new("jane.doe", "Jane Doe", "jane.doe@example.com", UserRole.Employee);

    private static SupportRequest CreateSupportRequest() =>
        new(
            "Printer not working",
            "The office printer jams.",
            RequestPriority.Medium,
            new RequestCategory("Hardware"),
            CreateUser());

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenContentIsBlank_ThrowsArgumentException(string content)
    {
        Assert.Throws<ArgumentException>(() =>
            new RequestComment(content, isInternal: false, CreateSupportRequest(), CreateUser()));
    }

    [Fact]
    public void Constructor_WhenContentIsNull_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new RequestComment(null!, isInternal: false, CreateSupportRequest(), CreateUser()));
    }

    [Fact]
    public void Constructor_WhenSupportRequestIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RequestComment("Looking into it.", isInternal: false, null!, CreateUser()));
    }

    [Fact]
    public void Constructor_WhenAuthorUserIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RequestComment("Looking into it.", isInternal: false, CreateSupportRequest(), null!));
    }

    [Fact]
    public void Constructor_WhenValuesAreValid_RetainsSuppliedValues()
    {
        var request = CreateSupportRequest();
        var author = CreateUser();

        var comment = new RequestComment("Looking into it.", isInternal: true, request, author);

        Assert.Equal("Looking into it.", comment.Content);
        Assert.True(comment.IsInternal);
        Assert.Same(request, comment.SupportRequest);
        Assert.Same(author, comment.AuthorUser);
    }
}
