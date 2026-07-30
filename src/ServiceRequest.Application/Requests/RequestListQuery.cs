using System.ComponentModel.DataAnnotations;
using ServiceRequest.Domain.Enums;

namespace ServiceRequest.Application.Requests;

public sealed class RequestListQuery : IValidatableObject
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public RequestStatus? Status { get; init; }

    public RequestPriority? Priority { get; init; }

    public int? CategoryId { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Page < 1)
        {
            yield return new ValidationResult("Page must be 1 or greater.", new[] { nameof(Page) });
        }

        if (PageSize is < 1 or > 100)
        {
            yield return new ValidationResult("Page size must be between 1 and 100.", new[] { nameof(PageSize) });
        }

        if (CategoryId is <= 0)
        {
            yield return new ValidationResult("Category id must be greater than zero.", new[] { nameof(CategoryId) });
        }
    }
}
