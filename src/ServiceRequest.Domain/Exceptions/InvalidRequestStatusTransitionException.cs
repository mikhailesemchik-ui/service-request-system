using ServiceRequest.Domain.Enums;

namespace ServiceRequest.Domain.Exceptions;

public sealed class InvalidRequestStatusTransitionException : DomainException
{
    public InvalidRequestStatusTransitionException(RequestStatus currentStatus, RequestStatus requestedStatus)
        : base($"This request cannot move from {currentStatus} to {requestedStatus}.")
    {
    }

    public InvalidRequestStatusTransitionException(string message)
        : base(message)
    {
    }
}
