using System.ComponentModel.DataAnnotations;
using ServiceRequest.Domain.Enums;

namespace ServiceRequest.Application.Requests;

public sealed class UpdateRequestClassificationRequest : IValidatableObject
{
    public int CategoryId { get; init; }

    public RequestPriority Priority { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CategoryId <= 0)
        {
            yield return new ValidationResult("Category is required.", new[] { nameof(CategoryId) });
        }
    }
}
