namespace ServiceRequest.Domain.Exceptions;

public sealed class RequestClassificationLockedException : DomainException
{
    public RequestClassificationLockedException()
        : base("Closed or cancelled requests cannot be changed.")
    {
    }
}
