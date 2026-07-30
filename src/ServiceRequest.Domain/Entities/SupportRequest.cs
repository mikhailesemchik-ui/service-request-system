using ServiceRequest.Domain.Enums;
using ServiceRequest.Domain.Exceptions;
using ServiceRequest.Domain.Rules;

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

    /// <returns><c>true</c> if the assignee actually changed; <c>false</c> for an idempotent no-op.</returns>
    public bool AssignTo(ApplicationUser assignee)
    {
        ArgumentNullException.ThrowIfNull(assignee);

        if (Status is RequestStatus.Closed or RequestStatus.Cancelled)
        {
            throw new InvalidRequestAssigneeException($"This request is {Status} and cannot be reassigned.");
        }

        if (AssignedToUserId == assignee.Id)
        {
            return false;
        }

        if (assignee.Role is not (UserRole.SupportAgent or UserRole.Admin) || !assignee.IsActive)
        {
            throw new InvalidRequestAssigneeException("Only active support staff can be assigned to a request.");
        }

        AssignedToUser = assignee;
        AssignedToUserId = assignee.Id;
        UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    /// <returns><c>true</c> if an assignment was actually removed; <c>false</c> for an idempotent no-op.</returns>
    public bool RemoveAssignment()
    {
        if (Status is RequestStatus.Closed or RequestStatus.Cancelled)
        {
            throw new InvalidRequestAssigneeException($"This request is {Status} and its assignment cannot change.");
        }

        if (AssignedToUserId is null)
        {
            return false;
        }

        AssignedToUser = null;
        AssignedToUserId = null;
        UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    /// <returns><c>true</c> if the status actually changed; <c>false</c> for an idempotent same-status no-op.</returns>
    public bool ChangeStatus(RequestStatus newStatus)
    {
        if (Status == newStatus)
        {
            return false;
        }

        if (!RequestStatusTransitions.CanTransition(Status, newStatus))
        {
            throw new InvalidRequestStatusTransitionException(Status, newStatus);
        }

        // A request cannot be "in progress" with nobody working on it; the caller must assign it
        // via a separate assignment call first rather than being auto-assigned here.
        if (newStatus == RequestStatus.InProgress && AssignedToUserId is null)
        {
            throw new InvalidRequestStatusTransitionException(
                $"This request must be assigned before it can move to {RequestStatus.InProgress}.");
        }

        Status = newStatus;
        ApplyLifecycleTimestamps(newStatus);
        UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    private void ApplyLifecycleTimestamps(RequestStatus newStatus)
    {
        var now = DateTimeOffset.UtcNow;

        switch (newStatus)
        {
            case RequestStatus.Resolved:
                ResolvedAt = now;
                ClosedAt = null;
                CancelledAt = null;
                break;
            case RequestStatus.Closed:
                ClosedAt = now;
                break;
            case RequestStatus.Cancelled:
                CancelledAt = now;
                ResolvedAt = null;
                ClosedAt = null;
                break;
            case RequestStatus.InProgress:
                // Covers reopening from Resolved; a no-op for New/WaitingForUser, which never set these.
                ResolvedAt = null;
                ClosedAt = null;
                CancelledAt = null;
                break;
            case RequestStatus.WaitingForUser:
                break;
        }
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
