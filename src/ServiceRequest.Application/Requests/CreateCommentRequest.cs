using System.ComponentModel.DataAnnotations;

namespace ServiceRequest.Application.Requests;

public sealed class CreateCommentRequest : IValidatableObject
{
    public string Content { get; init; } = string.Empty;

    public bool IsInternal { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Content))
        {
            yield return new ValidationResult(
                "Comment content cannot be empty.",
                new[] { nameof(Content) });

            yield break;
        }

        if (Content.Length > 4000)
        {
            yield return new ValidationResult(
                "Comment cannot exceed 4000 characters.",
                new[] { nameof(Content) });
        }
    }
}
