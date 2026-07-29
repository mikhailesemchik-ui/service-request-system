namespace ServiceRequest.Application.Categories;

public sealed record CategoryDto(
    int Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
