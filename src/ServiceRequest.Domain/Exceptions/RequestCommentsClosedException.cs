namespace ServiceRequest.Domain.Exceptions;

public sealed class RequestCommentsClosedException : DomainException
{
    public RequestCommentsClosedException(string requestStatus)
        : base($"New comments cannot be added to a request that is {requestStatus}.")
    {
    }
}
