using System.ComponentModel.DataAnnotations;
using ServiceRequest.Domain.Entities;

namespace ServiceRequest.Application.Requests;

public sealed class UpdateRequestContentRequest : IValidatableObject
{
    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var trimmedTitle = Title?.Trim() ?? string.Empty;

        if (trimmedTitle.Length is < SupportRequest.MinimumTitleLength or > SupportRequest.MaximumTitleLength)
        {
            yield return new ValidationResult(
                $"Title must contain between {SupportRequest.MinimumTitleLength} and {SupportRequest.MaximumTitleLength} characters.",
                new[] { nameof(Title) });
        }

        var trimmedDescription = Description?.Trim() ?? string.Empty;

        if (trimmedDescription.Length is < SupportRequest.MinimumDescriptionLength or > SupportRequest.MaximumDescriptionLength)
        {
            yield return new ValidationResult(
                $"Description must contain between {SupportRequest.MinimumDescriptionLength} and {SupportRequest.MaximumDescriptionLength} characters.",
                new[] { nameof(Description) });
        }
    }
}
