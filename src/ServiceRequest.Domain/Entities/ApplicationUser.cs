using ServiceRequest.Domain.Enums;

namespace ServiceRequest.Domain.Entities;

public sealed class ApplicationUser
{
    public int Id { get; private set; }

    public string Username { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private ApplicationUser()
    {
    }

    public ApplicationUser(string username, string displayName, string email, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be blank.", nameof(username));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name cannot be blank.", nameof(displayName));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be blank.", nameof(email));
        }

        Username = username;
        DisplayName = displayName;
        Email = email;
        Role = role;
        IsActive = true;

        var now = DateTimeOffset.UtcNow;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash cannot be blank.", nameof(passwordHash));
        }

        PasswordHash = passwordHash;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActiveState(bool isActive)
    {
        if (IsActive == isActive)
        {
            return;
        }

        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
