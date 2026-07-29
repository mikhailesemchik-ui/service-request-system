namespace ServiceRequest.Application.Categories;

public interface IRequestCategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<CategoryDto> GetByIdAsync(int categoryId, CancellationToken cancellationToken);

    Task<CategoryDto> CreateAsync(string name, string? description, CancellationToken cancellationToken);

    Task<CategoryDto> UpdateAsync(int categoryId, string name, string? description, CancellationToken cancellationToken);

    Task<CategoryDto> SetActiveStateAsync(int categoryId, bool isActive, CancellationToken cancellationToken);
}
