namespace ServiceRequest.Domain.Exceptions;

public sealed class RequestClassificationForbiddenException : DomainException
{
    public RequestClassificationForbiddenException()
        : base("You do not have permission to edit this request.")
    {
    }
}
