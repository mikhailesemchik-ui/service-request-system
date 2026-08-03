namespace ServiceRequest.Domain.Exceptions;

public sealed class RequestContentLockedException : DomainException
{
    public RequestContentLockedException(string message = "Closed or cancelled requests cannot be changed.")
        : base(message)
    {
    }
}
