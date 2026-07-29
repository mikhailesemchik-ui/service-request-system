namespace ServiceRequest.Domain.Exceptions;

public sealed class DuplicateRequestCategoryNameException : DomainException
{
    public DuplicateRequestCategoryNameException(string name)
        : base($"A category named \"{name}\" already exists.")
    {
    }
}
