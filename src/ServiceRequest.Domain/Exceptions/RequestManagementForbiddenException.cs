namespace ServiceRequest.Domain.Exceptions;

public sealed class RequestManagementForbiddenException : DomainException
{
    public RequestManagementForbiddenException(string message)
        : base(message)
    {
    }
}
