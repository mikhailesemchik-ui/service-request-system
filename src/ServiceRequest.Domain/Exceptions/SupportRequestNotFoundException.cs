namespace ServiceRequest.Domain.Exceptions;

public sealed class SupportRequestNotFoundException : DomainException
{
    public SupportRequestNotFoundException(int requestId)
        : base($"Request {requestId} was not found.")
    {
    }
}
