using Microsoft.EntityFrameworkCore;
using ServiceRequest.Application.Authentication;
using ServiceRequest.Application.Common;
using ServiceRequest.Application.Requests;
using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;
using ServiceRequest.Domain.Exceptions;
using ServiceRequest.Infrastructure.Data;

namespace ServiceRequest.Infrastructure.Requests;

public sealed class RequestService : IRequestService
{
    private readonly ApplicationDbContext _dbContext;

    public RequestService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<RequestListItemDto>> GetListAsync(
        RequestListQuery query,
        AuthenticatedUserDto currentUser,
        CancellationToken cancellationToken)
    {
        var requests = _dbContext.SupportRequests.AsNoTracking();

        if (!IsStaffRole(currentUser.Role))
        {
            requests = requests.Where(request => request.CreatedByUserId == currentUser.Id);
        }

        if (query.Status is { } status)
        {
            requests = requests.Where(request => request.Status == status);
        }

        if (query.Priority is { } priority)
        {
            requests = requests.Where(request => request.Priority == priority);
        }

        if (query.CategoryId is { } categoryId)
        {
            requests = requests.Where(request => request.CategoryId == categoryId);
        }

        var totalCount = await requests.CountAsync(cancellationToken);

        var items = await requests
            .OrderByDescending(request => request.CreatedAt)
            .ThenByDescending(request => request.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(request => new RequestListItemDto(
                request.Id,
                request.Title,
                request.Status,
                request.Priority,
                new RequestCategorySummaryDto(request.Category.Id, request.Category.Name),
                new RequestUserSummaryDto(request.CreatedByUser.Id, request.CreatedByUser.DisplayName),
                request.AssignedToUser == null
                    ? null
                    : new RequestUserSummaryDto(request.AssignedToUser.Id, request.AssignedToUser.DisplayName),
                request.CreatedAt,
                request.UpdatedAt))
            .ToListAsync(cancellationToken);

        return PagedResult<RequestListItemDto>.Create(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<RequestDetailsDto> GetByIdAsync(
        int requestId,
        AuthenticatedUserDto currentUser,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.SupportRequests.AsNoTracking().Where(request => request.Id == requestId);

        if (!IsStaffRole(currentUser.Role))
        {
            query = query.Where(request => request.CreatedByUserId == currentUser.Id);
        }

        var details = await query
            .Select(request => new RequestDetailsDto(
                request.Id,
                request.Title,
                request.Description,
                request.Status,
                request.Priority,
                new RequestCategorySummaryDto(request.Category.Id, request.Category.Name),
                new RequestUserSummaryDto(request.CreatedByUser.Id, request.CreatedByUser.DisplayName),
                request.AssignedToUser == null
                    ? null
                    : new RequestUserSummaryDto(request.AssignedToUser.Id, request.AssignedToUser.DisplayName),
                request.CreatedAt,
                request.UpdatedAt,
                request.ResolvedAt,
                request.ClosedAt,
                request.CancelledAt))
            .SingleOrDefaultAsync(cancellationToken);

        return details ?? throw new SupportRequestNotFoundException(requestId);
    }

    public async Task<RequestDetailsDto> CreateAsync(
        CreateRequestRequest request,
        AuthenticatedUserDto currentUser,
        CancellationToken cancellationToken)
    {
        var category = await _dbContext.RequestCategories
            .SingleOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken)
            ?? throw new RequestCategoryNotFoundException(request.CategoryId);

        if (!category.IsActive)
        {
            throw new RequestCategoryInactiveException(category.Id);
        }

        var creator = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == currentUser.Id, cancellationToken)
            ?? throw new CurrentUserUnavailableException();

        if (!creator.IsActive)
        {
            throw new CurrentUserUnavailableException();
        }

        var supportRequest = new SupportRequest(request.Title, request.Description, request.Priority, category, creator);

        _dbContext.SupportRequests.Add(supportRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDetailsDto(supportRequest);
    }

    private static bool IsStaffRole(string role) =>
        role == nameof(UserRole.SupportAgent) || role == nameof(UserRole.Admin);

    private static RequestDetailsDto ToDetailsDto(SupportRequest request) =>
        new(
            request.Id,
            request.Title,
            request.Description,
            request.Status,
            request.Priority,
            new RequestCategorySummaryDto(request.Category.Id, request.Category.Name),
            new RequestUserSummaryDto(request.CreatedByUser.Id, request.CreatedByUser.DisplayName),
            request.AssignedToUser == null
                ? null
                : new RequestUserSummaryDto(request.AssignedToUser.Id, request.AssignedToUser.DisplayName),
            request.CreatedAt,
            request.UpdatedAt,
            request.ResolvedAt,
            request.ClosedAt,
            request.CancelledAt);
}
