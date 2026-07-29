using ServiceRequest.Domain.Entities;

namespace ServiceRequest.UnitTests.Domain.Entities;

public class RequestCategoryTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenNameIsBlank_ThrowsArgumentException(string name)
    {
        Assert.Throws<ArgumentException>(() => new RequestCategory(name));
    }

    [Fact]
    public void Constructor_WhenNameIsNull_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new RequestCategory(null!));
    }

    [Fact]
    public void Constructor_WhenValuesAreValid_RetainsSuppliedValues()
    {
        var category = new RequestCategory("Hardware", "Hardware-related requests");

        Assert.Equal("Hardware", category.Name);
        Assert.Equal("Hardware-related requests", category.Description);
        Assert.True(category.IsActive);
    }

    [Fact]
    public void Constructor_WhenNameHasSurroundingWhitespace_TrimsName()
    {
        var category = new RequestCategory("  Hardware  ", "Description");

        Assert.Equal("Hardware", category.Name);
    }

    [Fact]
    public void Constructor_WhenDescriptionIsWhitespaceOnly_ConvertsToNull()
    {
        var category = new RequestCategory("Hardware", "   ");

        Assert.Null(category.Description);
    }

    [Fact]
    public void UpdateDetails_WhenValuesAreValid_ChangesNameAndDescription()
    {
        var category = new RequestCategory("Hardware", "Original description");

        category.UpdateDetails("Hardware Support", "Updated description");

        Assert.Equal("Hardware Support", category.Name);
        Assert.Equal("Updated description", category.Description);
    }

    [Fact]
    public void UpdateDetails_WhenValuesHaveSurroundingWhitespace_TrimsValues()
    {
        var category = new RequestCategory("Hardware", "Original description");

        category.UpdateDetails("  Hardware Support  ", "  Updated description  ");

        Assert.Equal("Hardware Support", category.Name);
        Assert.Equal("Updated description", category.Description);
    }

    [Fact]
    public void UpdateDetails_WhenDescriptionIsWhitespaceOnly_ConvertsToNull()
    {
        var category = new RequestCategory("Hardware", "Original description");

        category.UpdateDetails("Hardware", "   ");

        Assert.Null(category.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDetails_WhenNameIsBlank_ThrowsArgumentException(string name)
    {
        var category = new RequestCategory("Hardware", "Description");

        Assert.Throws<ArgumentException>(() => category.UpdateDetails(name, "Description"));
    }

    [Fact]
    public void UpdateDetails_WhenCalled_UpdatesUpdatedAt()
    {
        var category = new RequestCategory("Hardware", "Description");
        var initialUpdatedAt = category.UpdatedAt;

        category.UpdateDetails("Hardware Support", "Updated description");

        Assert.True(category.UpdatedAt >= initialUpdatedAt);
    }

    [Fact]
    public void SetActiveState_WhenSetToFalse_DeactivatesCategory()
    {
        var category = new RequestCategory("Hardware", "Description");

        category.SetActiveState(false);

        Assert.False(category.IsActive);
    }

    [Fact]
    public void SetActiveState_WhenSetToTrueAfterDeactivation_ReactivatesCategory()
    {
        var category = new RequestCategory("Hardware", "Description");
        category.SetActiveState(false);

        category.SetActiveState(true);

        Assert.True(category.IsActive);
    }

    [Fact]
    public void SetActiveState_WhenCalledRepeatedlyWithSameState_RemainsValid()
    {
        var category = new RequestCategory("Hardware", "Description");

        category.SetActiveState(false);
        category.SetActiveState(false);
        category.SetActiveState(false);

        Assert.False(category.IsActive);
    }

    [Fact]
    public void SetActiveState_WhenStateActuallyChanges_UpdatesUpdatedAt()
    {
        var category = new RequestCategory("Hardware", "Description");
        var initialUpdatedAt = category.UpdatedAt;

        category.SetActiveState(false);

        Assert.True(category.UpdatedAt >= initialUpdatedAt);
    }
}
