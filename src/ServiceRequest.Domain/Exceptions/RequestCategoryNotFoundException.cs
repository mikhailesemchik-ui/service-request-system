namespace ServiceRequest.Domain.Exceptions;

public sealed class RequestCategoryNotFoundException : DomainException
{
    public RequestCategoryNotFoundException(int categoryId)
        : base($"Category {categoryId} was not found.")
    {
    }
}
