using System.ComponentModel.DataAnnotations;

namespace ServiceRequest.Application.Categories;

public sealed class CreateCategoryRequest : IValidatableObject
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var trimmedName = Name?.Trim() ?? string.Empty;

        if (trimmedName.Length is < 2 or > 100)
        {
            yield return new ValidationResult(
                "Name must contain between 2 and 100 characters.",
                new[] { nameof(Name) });
        }

        var trimmedDescription = Description?.Trim();

        if (trimmedDescription is { Length: > 500 })
        {
            yield return new ValidationResult(
                "Description must not exceed 500 characters.",
                new[] { nameof(Description) });
        }
    }
}
