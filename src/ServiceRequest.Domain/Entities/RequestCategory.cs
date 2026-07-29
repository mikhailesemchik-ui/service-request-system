namespace ServiceRequest.Domain.Entities;

public sealed class RequestCategory
{
    public int Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private RequestCategory()
    {
    }

    public RequestCategory(string name, string? description = null)
    {
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        IsActive = true;

        var now = DateTimeOffset.UtcNow;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public void UpdateDetails(string name, string? description)
    {
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActiveState(bool isActive)
    {
        if (IsActive == isActive)
        {
            return;
        }

        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string NormalizeName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Category name cannot be blank.", nameof(name));
        }

        return trimmed;
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
