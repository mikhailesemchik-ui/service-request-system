namespace ServiceRequest.Domain.Exceptions;

public sealed class RequestContentForbiddenException : DomainException
{
    public RequestContentForbiddenException()
        : base("You do not have permission to edit this request.")
    {
    }
}
