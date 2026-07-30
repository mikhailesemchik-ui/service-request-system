using System.ComponentModel.DataAnnotations;

namespace ServiceRequest.Application.Requests;

public sealed class UpdateRequestAssignmentRequest : IValidatableObject
{
    /// <summary>A <c>null</c> value removes the current assignment.</summary>
    public int? AssignedToUserId { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AssignedToUserId is <= 0)
        {
            yield return new ValidationResult(
                "Assignee id must be greater than zero.",
                new[] { nameof(AssignedToUserId) });
        }
    }
}
