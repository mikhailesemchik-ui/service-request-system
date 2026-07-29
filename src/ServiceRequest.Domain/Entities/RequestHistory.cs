namespace ServiceRequest.Domain.Entities;

public sealed class RequestHistory
{
    public int Id { get; private set; }

    public int ServiceRequestId { get; private set; }

    public SupportRequest SupportRequest { get; private set; } = null!;

    public int ChangedByUserId { get; private set; }

    public ApplicationUser ChangedByUser { get; private set; } = null!;

    public string Action { get; private set; } = string.Empty;

    public string? PreviousValue { get; private set; }

    public string? NewValue { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private RequestHistory()
    {
    }

    public RequestHistory(
        SupportRequest supportRequest,
        ApplicationUser changedByUser,
        string action,
        string? previousValue,
        string? newValue)
    {
        ArgumentNullException.ThrowIfNull(supportRequest);
        ArgumentNullException.ThrowIfNull(changedByUser);

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Action cannot be blank.", nameof(action));
        }

        SupportRequest = supportRequest;
        ServiceRequestId = supportRequest.Id;
        ChangedByUser = changedByUser;
        ChangedByUserId = changedByUser.Id;
        Action = action;
        PreviousValue = previousValue;
        NewValue = newValue;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
