namespace ServiceRequest.Domain.Exceptions;

public sealed class InvalidRequestAssigneeException : DomainException
{
    public InvalidRequestAssigneeException(string message)
        : base(message)
    {
    }
}
