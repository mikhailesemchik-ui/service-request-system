using System.ComponentModel.DataAnnotations;

namespace ServiceRequest.Application.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required]
    [MinLength(32, ErrorMessage = "Jwt:SigningKey must be at least 32 characters long.")]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Jwt:ExpirationMinutes must be greater than zero.")]
    public int ExpirationMinutes { get; set; } = 60;
}
