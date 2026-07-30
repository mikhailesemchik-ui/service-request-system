using ServiceRequest.Domain.Enums;

namespace ServiceRequest.Domain.Entities;

public sealed class SupportRequest
{
    public int Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public RequestStatus Status { get; private set; }

    public RequestPriority Priority { get; private set; }

    public int CategoryId { get; private set; }

    public RequestCategory Category { get; private set; } = null!;

    public int CreatedByUserId { get; private set; }

    public ApplicationUser CreatedByUser { get; private set; } = null!;

    public int? AssignedToUserId { get; private set; }

    public ApplicationUser? AssignedToUser { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    private SupportRequest()
    {
    }

    public SupportRequest(
        string title,
        string description,
        RequestPriority priority,
        RequestCategory category,
        ApplicationUser createdByUser)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(createdByUser);

        Title = NormalizeRequiredText(title, "Request title cannot be blank.", nameof(title));
        Description = NormalizeRequiredText(description, "Request description cannot be blank.", nameof(description));
        Status = RequestStatus.New;
        Priority = priority;
        Category = category;
        CategoryId = category.Id;
        CreatedByUser = createdByUser;
        CreatedByUserId = createdByUser.Id;

        var now = DateTimeOffset.UtcNow;
        CreatedAt = now;
        UpdatedAt = now;
    }

    private static string NormalizeRequiredText(string value, string errorMessage, string paramName)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException(errorMessage, paramName);
        }

        return trimmed;
    }
}
