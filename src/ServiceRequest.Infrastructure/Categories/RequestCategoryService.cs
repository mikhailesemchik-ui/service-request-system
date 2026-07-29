using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ServiceRequest.Application.Categories;
using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Exceptions;
using ServiceRequest.Infrastructure.Data;

namespace ServiceRequest.Infrastructure.Categories;

public sealed class RequestCategoryService : IRequestCategoryService
{
    private readonly ApplicationDbContext _dbContext;

    public RequestCategoryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var query = _dbContext.RequestCategories.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(category => category.IsActive);
        }

        return await query
            .OrderBy(category => category.Name)
            .Select(category => new CategoryDto(
                category.Id,
                category.Name,
                category.Description,
                category.IsActive,
                category.CreatedAt,
                category.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryDto> GetByIdAsync(int categoryId, CancellationToken cancellationToken)
    {
        var category = await _dbContext.RequestCategories
            .AsNoTracking()
            .Where(c => c.Id == categoryId)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.IsActive, c.CreatedAt, c.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        return category ?? throw new RequestCategoryNotFoundException(categoryId);
    }

    public async Task<CategoryDto> CreateAsync(string name, string? description, CancellationToken cancellationToken)
    {
        var trimmedName = name.Trim();

        await EnsureNameIsUniqueAsync(trimmedName, excludingCategoryId: null, cancellationToken);

        var category = new RequestCategory(name, description);
        _dbContext.RequestCategories.Add(category);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new DuplicateRequestCategoryNameException(trimmedName);
        }

        return ToDto(category);
    }

    public async Task<CategoryDto> UpdateAsync(int categoryId, string name, string? description, CancellationToken cancellationToken)
    {
        var category = await _dbContext.RequestCategories
            .SingleOrDefaultAsync(c => c.Id == categoryId, cancellationToken)
            ?? throw new RequestCategoryNotFoundException(categoryId);

        var trimmedName = name.Trim();

        await EnsureNameIsUniqueAsync(trimmedName, excludingCategoryId: categoryId, cancellationToken);

        category.UpdateDetails(name, description);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new DuplicateRequestCategoryNameException(trimmedName);
        }

        return ToDto(category);
    }

    public async Task<CategoryDto> SetActiveStateAsync(int categoryId, bool isActive, CancellationToken cancellationToken)
    {
        var category = await _dbContext.RequestCategories
            .SingleOrDefaultAsync(c => c.Id == categoryId, cancellationToken)
            ?? throw new RequestCategoryNotFoundException(categoryId);

        category.SetActiveState(isActive);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(category);
    }

    private async Task EnsureNameIsUniqueAsync(string trimmedName, int? excludingCategoryId, CancellationToken cancellationToken)
    {
        var nameExists = await _dbContext.RequestCategories
            .AsNoTracking()
            .Where(c => excludingCategoryId == null || c.Id != excludingCategoryId)
            .AnyAsync(c => c.Name == trimmedName, cancellationToken);

        if (nameExists)
        {
            throw new DuplicateRequestCategoryNameException(trimmedName);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteErrorCode: 19 };

    private static CategoryDto ToDto(RequestCategory category) =>
        new(category.Id, category.Name, category.Description, category.IsActive, category.CreatedAt, category.UpdatedAt);
}
