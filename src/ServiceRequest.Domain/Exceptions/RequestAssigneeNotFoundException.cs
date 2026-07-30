namespace ServiceRequest.Domain.Exceptions;

public sealed class RequestAssigneeNotFoundException : DomainException
{
    public RequestAssigneeNotFoundException(int userId)
        : base($"User {userId} was not found.")
    {
    }
}
