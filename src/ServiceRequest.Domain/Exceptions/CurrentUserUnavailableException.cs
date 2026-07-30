namespace ServiceRequest.Domain.Exceptions;

public sealed class CurrentUserUnavailableException : DomainException
{
    public CurrentUserUnavailableException()
        : base("The current user could not be found or is no longer active.")
    {
    }
}
