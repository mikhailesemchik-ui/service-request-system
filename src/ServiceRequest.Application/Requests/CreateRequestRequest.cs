using System.ComponentModel.DataAnnotations;
using ServiceRequest.Domain.Enums;

namespace ServiceRequest.Application.Requests;

public sealed class CreateRequestRequest : IValidatableObject
{
    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public int CategoryId { get; init; }

    public RequestPriority Priority { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var trimmedTitle = Title?.Trim() ?? string.Empty;

        if (trimmedTitle.Length is < 3 or > 200)
        {
            yield return new ValidationResult(
                "Title must contain between 3 and 200 characters.",
                new[] { nameof(Title) });
        }

        var trimmedDescription = Description?.Trim() ?? string.Empty;

        if (trimmedDescription.Length is < 10 or > 4000)
        {
            yield return new ValidationResult(
                "Description must contain between 10 and 4000 characters.",
                new[] { nameof(Description) });
        }

        if (CategoryId <= 0)
        {
            yield return new ValidationResult(
                "Category id must be greater than zero.",
                new[] { nameof(CategoryId) });
        }
    }
}
