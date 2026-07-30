namespace ServiceRequest.Domain.Exceptions;

public sealed class RequestCategoryInactiveException : DomainException
{
    public RequestCategoryInactiveException(int categoryId)
        : base($"Category {categoryId} is not active.")
    {
    }
}
