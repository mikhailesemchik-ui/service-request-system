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

    public async Task<RequestDetailsDto> SetAssignmentAsync(
        int requestId,
        UpdateRequestAssignmentRequest request,
        AuthenticatedUserDto currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Role == nameof(UserRole.Employee))
        {
            throw new RequestManagementForbiddenException("You do not have permission to manage assignments.");
        }

        var supportRequest = await LoadTrackedRequestAsync(requestId, cancellationToken)
            ?? throw new SupportRequestNotFoundException(requestId);

        var actor = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == currentUser.Id, cancellationToken)
            ?? throw new CurrentUserUnavailableException();

        ApplicationUser? newAssignee = null;

        if (request.AssignedToUserId is { } assigneeId)
        {
            newAssignee = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == assigneeId, cancellationToken)
                ?? throw new RequestAssigneeNotFoundException(assigneeId);
        }

        if (currentUser.Role == nameof(UserRole.SupportAgent))
        {
            EnsureSupportAgentCanManageAssignment(supportRequest, currentUser.Id, newAssignee);
        }

        var previousAssigneeId = supportRequest.AssignedToUserId;
        var changed = newAssignee is null ? supportRequest.RemoveAssignment() : supportRequest.AssignTo(newAssignee);

        if (changed)
        {
            _dbContext.RequestHistory.Add(new RequestHistory(
                supportRequest,
                actor,
                RequestHistoryActions.AssignmentChanged,
                previousAssigneeId?.ToString(),
                newAssignee?.Id.ToString()));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDetailsDto(supportRequest);
    }

    public async Task<RequestDetailsDto> ChangeStatusAsync(
        int requestId,
        UpdateRequestStatusRequest request,
        AuthenticatedUserDto currentUser,
        CancellationToken cancellationToken)
    {
        var supportRequest = await LoadTrackedRequestAsync(requestId, cancellationToken)
            ?? throw new SupportRequestNotFoundException(requestId);

        var actor = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == currentUser.Id, cancellationToken)
            ?? throw new CurrentUserUnavailableException();

        var isEmployee = currentUser.Role == nameof(UserRole.Employee);

        // Hidden resource: an employee must never learn that another employee's request exists.
        if (isEmployee && supportRequest.CreatedByUserId != currentUser.Id)
        {
            throw new SupportRequestNotFoundException(requestId);
        }

        var previousStatus = supportRequest.Status;
        var targetStatus = request.Status;

        if (previousStatus != targetStatus)
        {
            EnsureActorCanSetStatus(supportRequest, currentUser, targetStatus);
        }

        var changed = supportRequest.ChangeStatus(targetStatus);

        if (changed)
        {
            _dbContext.RequestHistory.Add(new RequestHistory(
                supportRequest,
                actor,
                RequestHistoryActions.StatusChanged,
                previousStatus.ToString(),
                targetStatus.ToString()));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDetailsDto(supportRequest);
    }

    public async Task<IReadOnlyList<RequestHistoryDto>> GetHistoryAsync(
        int requestId,
        AuthenticatedUserDto currentUser,
        CancellationToken cancellationToken)
    {
        var requestQuery = _dbContext.SupportRequests.AsNoTracking().Where(request => request.Id == requestId);

        if (!IsStaffRole(currentUser.Role))
        {
            requestQuery = requestQuery.Where(request => request.CreatedByUserId == currentUser.Id);
        }

        if (!await requestQuery.AnyAsync(cancellationToken))
        {
            throw new SupportRequestNotFoundException(requestId);
        }

        var entries = await _dbContext.RequestHistory
            .AsNoTracking()
            .Where(history => history.ServiceRequestId == requestId)
            .OrderBy(history => history.CreatedAt)
            .ThenBy(history => history.Id)
            .Select(history => new
            {
                history.Id,
                history.Action,
                history.PreviousValue,
                history.NewValue,
                history.CreatedAt,
                ChangedBy = new RequestUserSummaryDto(history.ChangedByUser.Id, history.ChangedByUser.DisplayName),
            })
            .ToListAsync(cancellationToken);

        var referencedUserIds = entries
            .Where(entry => entry.Action == RequestHistoryActions.AssignmentChanged)
            .SelectMany(entry => new[] { entry.PreviousValue, entry.NewValue })
            .Where(value => value != null)
            .Select(value => int.Parse(value!))
            .Distinct()
            .ToList();

        var userDisplayNames = referencedUserIds.Count == 0
            ? new Dictionary<int, string>()
            : await _dbContext.Users
                .AsNoTracking()
                .Where(user => referencedUserIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);

        var referencedCategoryIds = entries
            .Where(entry => entry.Action == RequestHistoryActions.CategoryChanged)
            .SelectMany(entry => new[] { entry.PreviousValue, entry.NewValue })
            .Where(value => value != null)
            .Select(value => int.Parse(value!))
            .Distinct()
            .ToList();

        var categoryNames = referencedCategoryIds.Count == 0
            ? new Dictionary<int, string>()
            : await _dbContext.RequestCategories
                .AsNoTracking()
                .Where(category => referencedCategoryIds.Contains(category.Id))
                .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);

        return entries
            .Select(entry => new RequestHistoryDto(
                entry.Id,
                entry.Action,
                entry.PreviousValue,
                entry.NewValue,
                ResolveDisplayValue(entry.Action, entry.PreviousValue, userDisplayNames, categoryNames),
                ResolveDisplayValue(entry.Action, entry.NewValue, userDisplayNames, categoryNames),
                entry.ChangedBy,
                entry.CreatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<RequestAssigneeDto>> GetEligibleAssigneesAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive && (user.Role == UserRole.SupportAgent || user.Role == UserRole.Admin))
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.Id)
            .Select(user => new RequestAssigneeDto(user.Id, user.DisplayName, user.Role))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RequestCommentDto>> GetCommentsAsync(
        int requestId,
        AuthenticatedUserDto currentUser,
        CancellationToken cancellationToken)
    {
        var requestQuery = _dbContext.SupportRequests.AsNoTracking().Where(r => r.Id == requestId);

        if (!IsStaffRole(currentUser.Role))
        {
            requestQuery = requestQuery.Where(r => r.CreatedByUserId == currentUser.Id);
        }

        if (!await requestQuery.AnyAsync(cancellationToken))
        {
            throw new SupportRequestNotFoundException(requestId);
        }

        var commentsQuery = _dbContext.RequestComments
            .AsNoTracking()
            .Where(c => c.ServiceRequestId == requestId);

        if (!IsStaffRole(currentUser.Role))
        {
            commentsQuery = commentsQuery.Where(c => !c.IsInternal);
        }

        return await commentsQuery
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Select(c => new RequestCommentDto(
                c.Id,
                c.Content,
                c.IsInternal,
                new RequestCommentAuthorDto(c.AuthorUser.Id, c.AuthorUser.DisplayName, c.AuthorUser.Role.ToString()),
                c.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<RequestCommentDto> AddCommentAsync(
        int requestId,
        CreateCommentRequest request,
        AuthenticatedUserDto currentUser,
        CancellationToken cancellationToken)
    {
        var requestQuery = _dbContext.SupportRequests.Where(r => r.Id == requestId);

        if (!IsStaffRole(currentUser.Role))
        {
            requestQuery = requestQuery.Where(r => r.CreatedByUserId == currentUser.Id);
        }

        var supportRequest = await requestQuery.SingleOrDefaultAsync(cancellationToken)
            ?? throw new SupportRequestNotFoundException(requestId);

        if (supportRequest.Status is RequestStatus.Closed or RequestStatus.Cancelled)
        {
            throw new RequestCommentsClosedException(supportRequest.Status.ToString());
        }

        if (!IsStaffRole(currentUser.Role) && request.IsInternal)
        {
            throw new InternalCommentForbiddenException();
        }

        var author = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == currentUser.Id, cancellationToken)
            ?? throw new CurrentUserUnavailableException();

        var comment = new RequestComment(request.Content, request.IsInternal, supportRequest, author);
        _dbContext.RequestComments.Add(comment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RequestCommentDto(
            comment.Id,
            comment.Content,
            comment.IsInternal,
            new RequestCommentAuthorDto(author.Id, author.DisplayName, author.Role.ToString()),
            comment.CreatedAt);
    }

    private Task<SupportRequest?> LoadTrackedRequestAsync(int requestId, CancellationToken cancellationToken) =>
        _dbContext.SupportRequests
            .Include(request => request.Category)
            .Include(request => request.CreatedByUser)
            .Include(request => request.AssignedToUser)
            .SingleOrDefaultAsync(request => request.Id == requestId, cancellationToken);

    private static void EnsureSupportAgentCanManageAssignment(
        SupportRequest supportRequest, int currentUserId, ApplicationUser? newAssignee)
    {
        if (newAssignee is not null && newAssignee.Id != currentUserId)
        {
            throw new RequestManagementForbiddenException("Support agents may only assign requests to themselves.");
        }

        if (supportRequest.AssignedToUserId is { } currentAssigneeId && currentAssigneeId != currentUserId)
        {
            throw new RequestManagementForbiddenException(
                newAssignee is null
                    ? "You can only remove your own assignment."
                    : "This request is assigned to another support agent.");
        }
    }

    private static void EnsureActorCanSetStatus(
        SupportRequest supportRequest, AuthenticatedUserDto currentUser, RequestStatus targetStatus)
    {
        if (currentUser.Role == nameof(UserRole.Employee))
        {
            var allowed = targetStatus is RequestStatus.Cancelled or RequestStatus.Closed;

            if (!allowed)
            {
                throw new RequestManagementForbiddenException(
                    $"You do not have permission to set this request's status to {targetStatus}.");
            }

            return;
        }

        if (currentUser.Role == nameof(UserRole.SupportAgent))
        {
            var allowed = targetStatus is RequestStatus.InProgress or RequestStatus.WaitingForUser
                or RequestStatus.Resolved or RequestStatus.Cancelled;

            if (!allowed)
            {
                throw new RequestManagementForbiddenException(
                    $"You do not have permission to set this request's status to {targetStatus}.");
            }

            if (supportRequest.AssignedToUserId != currentUser.Id)
            {
                throw new RequestManagementForbiddenException(
                    "You can only change the status of requests assigned to you.");
            }
        }
    }

    public async Task<RequestDetailsDto> UpdateClassificationAsync(
        int requestId,
        UpdateRequestClassificationRequest request,
        AuthenticatedUserDto currentUser,
        CancellationToken cancellationToken)
    {
        var actor = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == currentUser.Id, cancellationToken)
            ?? throw new CurrentUserUnavailableException();

        var supportRequest = await LoadTrackedRequestAsync(requestId, cancellationToken)
            ?? throw new SupportRequestNotFoundException(requestId);

        if (currentUser.Role == nameof(UserRole.Employee))
        {
            throw new RequestClassificationForbiddenException();
        }

        if (currentUser.Role == nameof(UserRole.SupportAgent) &&
            supportRequest.AssignedToUserId != currentUser.Id)
        {
            throw new RequestClassificationForbiddenException();
        }

        if (supportRequest.Status is RequestStatus.Closed or RequestStatus.Cancelled)
        {
            throw new RequestClassificationLockedException();
        }

        var newCategory = await _dbContext.RequestCategories
            .SingleOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken)
            ?? throw new RequestCategoryNotFoundException(request.CategoryId);

        if (!newCategory.IsActive)
        {
            throw new RequestCategoryInactiveException(newCategory.Id);
        }

        var previousCategoryId = supportRequest.CategoryId;
        var previousPriority = supportRequest.Priority;

        var categoryChanged = supportRequest.ChangeCategory(newCategory);
        var priorityChanged = supportRequest.ChangePriority(request.Priority);

        if (categoryChanged)
        {
            _dbContext.RequestHistory.Add(new RequestHistory(
                supportRequest,
                actor,
                RequestHistoryActions.CategoryChanged,
                previousCategoryId.ToString(),
                newCategory.Id.ToString()));
        }

        if (priorityChanged)
        {
            _dbContext.RequestHistory.Add(new RequestHistory(
                supportRequest,
                actor,
                RequestHistoryActions.PriorityChanged,
                previousPriority.ToString(),
                request.Priority.ToString()));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDetailsDto(supportRequest);
    }

    private static string? ResolveDisplayValue(
        string action,
        string? rawValue,
        IReadOnlyDictionary<int, string> userDisplayNames,
        IReadOnlyDictionary<int, string> categoryNames)
    {
        if (rawValue is null)
        {
            return null;
        }

        if (action == RequestHistoryActions.AssignmentChanged && int.TryParse(rawValue, out var userId))
        {
            return userDisplayNames.TryGetValue(userId, out var displayName) ? displayName : rawValue;
        }

        if (action == RequestHistoryActions.CategoryChanged && int.TryParse(rawValue, out var categoryId))
        {
            return categoryNames.TryGetValue(categoryId, out var categoryName) ? categoryName : rawValue;
        }

        return rawValue;
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
