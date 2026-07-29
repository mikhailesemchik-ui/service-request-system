namespace ServiceRequest.Domain.Enums;

public enum RequestStatus
{
    New,
    InProgress,
    WaitingForUser,
    Resolved,
    Closed,
    Cancelled,
}
