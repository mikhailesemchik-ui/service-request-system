using System.ComponentModel.DataAnnotations;

namespace ServiceRequest.Application.Categories;

public sealed class UpdateCategoryActiveStateRequest
{
    [Required]
    public bool? IsActive { get; init; }
}
