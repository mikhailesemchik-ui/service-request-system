using System.ComponentModel.DataAnnotations;

namespace ServiceRequest.Application.Authentication;

public sealed class LoginRequest : IValidatableObject
{
    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            yield return new ValidationResult("Username is required.", new[] { nameof(Username) });
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            yield return new ValidationResult("Password is required.", new[] { nameof(Password) });
        }
    }
}
