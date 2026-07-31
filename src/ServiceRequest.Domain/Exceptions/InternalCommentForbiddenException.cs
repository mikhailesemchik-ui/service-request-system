namespace ServiceRequest.Domain.Exceptions;

public sealed class InternalCommentForbiddenException : DomainException
{
    public InternalCommentForbiddenException()
        : base("You do not have permission to add internal comments.")
    {
    }
}
