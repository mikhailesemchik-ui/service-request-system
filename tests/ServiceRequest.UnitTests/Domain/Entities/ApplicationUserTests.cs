using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;

namespace ServiceRequest.UnitTests.Domain.Entities;

public class ApplicationUserTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenUsernameIsBlank_ThrowsArgumentException(string username)
    {
        Assert.Throws<ArgumentException>(() =>
            new ApplicationUser(username, "Jane Doe", "jane.doe@example.com", UserRole.Employee));
    }

    [Fact]
    public void Constructor_WhenUsernameIsNull_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new ApplicationUser(null!, "Jane Doe", "jane.doe@example.com", UserRole.Employee));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenDisplayNameIsBlank_ThrowsArgumentException(string displayName)
    {
        Assert.Throws<ArgumentException>(() =>
            new ApplicationUser("jane.doe", displayName, "jane.doe@example.com", UserRole.Employee));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenEmailIsBlank_ThrowsArgumentException(string email)
    {
        Assert.Throws<ArgumentException>(() =>
            new ApplicationUser("jane.doe", "Jane Doe", email, UserRole.Employee));
    }

    [Fact]
    public void Constructor_WhenValuesAreValid_RetainsSuppliedValues()
    {
        var user = new ApplicationUser("jane.doe", "Jane Doe", "jane.doe@example.com", UserRole.SupportAgent);

        Assert.Equal("jane.doe", user.Username);
        Assert.Equal("Jane Doe", user.DisplayName);
        Assert.Equal("jane.doe@example.com", user.Email);
        Assert.Equal(UserRole.SupportAgent, user.Role);
        Assert.True(user.IsActive);
    }

    [Fact]
    public void SetPasswordHash_WhenValid_SetsHash()
    {
        var user = new ApplicationUser("jane.doe", "Jane Doe", "jane.doe@example.com", UserRole.Employee);

        user.SetPasswordHash("AQAAAAIAAYagAAAAEhash-value==");

        Assert.Equal("AQAAAAIAAYagAAAAEhash-value==", user.PasswordHash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SetPasswordHash_WhenBlank_ThrowsArgumentException(string hash)
    {
        var user = new ApplicationUser("jane.doe", "Jane Doe", "jane.doe@example.com", UserRole.Employee);

        Assert.Throws<ArgumentException>(() => user.SetPasswordHash(hash));
    }

    [Fact]
    public void SetPasswordHash_WhenNull_ThrowsArgumentException()
    {
        var user = new ApplicationUser("jane.doe", "Jane Doe", "jane.doe@example.com", UserRole.Employee);

        Assert.Throws<ArgumentException>(() => user.SetPasswordHash(null!));
    }

    [Fact]
    public void SetPasswordHash_WhenCalledAgain_ReplacesStoredHash()
    {
        var user = new ApplicationUser("jane.doe", "Jane Doe", "jane.doe@example.com", UserRole.Employee);
        user.SetPasswordHash("first-hash-value");

        user.SetPasswordHash("second-hash-value");

        Assert.Equal("second-hash-value", user.PasswordHash);
    }

    [Fact]
    public void SetPasswordHash_DoesNotTrimOrNormalizeValue()
    {
        var user = new ApplicationUser("jane.doe", "Jane Doe", "jane.doe@example.com", UserRole.Employee);

        user.SetPasswordHash(" hash-with-surrounding-whitespace ");

        Assert.Equal(" hash-with-surrounding-whitespace ", user.PasswordHash);
    }
}
