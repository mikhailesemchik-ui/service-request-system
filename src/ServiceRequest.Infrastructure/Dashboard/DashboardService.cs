using Microsoft.EntityFrameworkCore;
using ServiceRequest.Application.Authentication;
using ServiceRequest.Application.Dashboard;
using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;
using ServiceRequest.Infrastructure.Data;

namespace ServiceRequest.Infrastructure.Dashboard;

public sealed class DashboardService : IDashboardService
{
    private static readonly RequestStatus[] ActiveStatuses =
        [RequestStatus.New, RequestStatus.InProgress, RequestStatus.WaitingForUser];

    private readonly ApplicationDbContext _dbContext;

    public DashboardService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<DashboardSummaryDto> GetSummaryAsync(
        AuthenticatedUserDto currentUser,
        CancellationToken cancellationToken)
    {
        return currentUser.Role switch
        {
            nameof(UserRole.Employee) => GetEmployeeSummaryAsync(currentUser.Id, cancellationToken),
            nameof(UserRole.SupportAgent) => GetStaffSummaryAsync(currentUser.Id, "SupportAgent", cancellationToken),
            _ => GetAdminSummaryAsync(currentUser.Id, cancellationToken),
        };
    }

    private async Task<DashboardSummaryDto> GetEmployeeSummaryAsync(
        int userId, CancellationToken cancellationToken)
    {
        var baseQuery = _dbContext.SupportRequests
            .AsNoTracking()
            .Where(r => r.CreatedByUserId == userId);

        var statusDict = await GetStatusCountDictAsync(baseQuery, cancellationToken);
        var priorityDict = await GetPriorityCountDictAsync(baseQuery, cancellationToken);
        var recentRequests = await GetRecentRequestsAsync(baseQuery, cancellationToken);

        return new DashboardSummaryDto(
            Scope: "Employee",
            TotalRequests: statusDict.Values.Sum(),
            OpenRequests: ActiveStatuses.Sum(s => statusDict.GetValueOrDefault(s, 0)),
            ResolvedRequests: statusDict.GetValueOrDefault(RequestStatus.Resolved, 0),
            ClosedRequests: statusDict.GetValueOrDefault(RequestStatus.Closed, 0),
            CancelledRequests: statusDict.GetValueOrDefault(RequestStatus.Cancelled, 0),
            StatusCounts: BuildStatusCounts(statusDict),
            PriorityCounts: BuildPriorityCounts(priorityDict),
            StaffMetrics: null,
            AdminMetrics: null,
            RecentRequests: recentRequests);
    }

    private async Task<DashboardSummaryDto> GetStaffSummaryAsync(
        int userId, string scope, CancellationToken cancellationToken)
    {
        var baseQuery = _dbContext.SupportRequests.AsNoTracking();

        var statusDict = await GetStatusCountDictAsync(baseQuery, cancellationToken);
        var priorityDict = await GetPriorityCountDictAsync(baseQuery, cancellationToken);
        var staffMetrics = await GetStaffMetricsAsync(userId, cancellationToken);
        var recentRequests = await GetRecentRequestsAsync(baseQuery, cancellationToken);

        return new DashboardSummaryDto(
            Scope: scope,
            TotalRequests: statusDict.Values.Sum(),
            OpenRequests: ActiveStatuses.Sum(s => statusDict.GetValueOrDefault(s, 0)),
            ResolvedRequests: statusDict.GetValueOrDefault(RequestStatus.Resolved, 0),
            ClosedRequests: statusDict.GetValueOrDefault(RequestStatus.Closed, 0),
            CancelledRequests: statusDict.GetValueOrDefault(RequestStatus.Cancelled, 0),
            StatusCounts: BuildStatusCounts(statusDict),
            PriorityCounts: BuildPriorityCounts(priorityDict),
            StaffMetrics: staffMetrics,
            AdminMetrics: null,
            RecentRequests: recentRequests);
    }

    private async Task<DashboardSummaryDto> GetAdminSummaryAsync(
        int userId, CancellationToken cancellationToken)
    {
        var staffSummary = await GetStaffSummaryAsync(userId, "Admin", cancellationToken);
        var adminMetrics = await GetAdminMetricsAsync(cancellationToken);

        return staffSummary with { AdminMetrics = adminMetrics };
    }

    private async Task<Dictionary<RequestStatus, int>> GetStatusCountDictAsync(
        IQueryable<SupportRequest> baseQuery, CancellationToken cancellationToken)
    {
        var raw = await baseQuery
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return raw.ToDictionary(x => x.Status, x => x.Count);
    }

    private async Task<Dictionary<RequestPriority, int>> GetPriorityCountDictAsync(
        IQueryable<SupportRequest> baseQuery, CancellationToken cancellationToken)
    {
        var raw = await baseQuery
            .GroupBy(r => r.Priority)
            .Select(g => new { Priority = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return raw.ToDictionary(x => x.Priority, x => x.Count);
    }

    private async Task<DashboardStaffMetricsDto> GetStaffMetricsAsync(
        int userId, CancellationToken cancellationToken)
    {
        var activeBase = _dbContext.SupportRequests
            .AsNoTracking()
            .Where(r => ActiveStatuses.Contains(r.Status));

        var unassignedActive = await activeBase
            .CountAsync(r => r.AssignedToUserId == null, cancellationToken);

        var assignedToMe = await _dbContext.SupportRequests
            .AsNoTracking()
            .CountAsync(r => r.AssignedToUserId == userId, cancellationToken);

        var activeAssignedToMe = await activeBase
            .CountAsync(r => r.AssignedToUserId == userId, cancellationToken);

        return new DashboardStaffMetricsDto(unassignedActive, assignedToMe, activeAssignedToMe);
    }

    private async Task<DashboardAdminMetricsDto> GetAdminMetricsAsync(CancellationToken cancellationToken)
    {
        var activeCategories = await _dbContext.RequestCategories
            .AsNoTracking()
            .CountAsync(c => c.IsActive, cancellationToken);

        var activeSupportAgents = await _dbContext.Users
            .AsNoTracking()
            .CountAsync(u => u.IsActive && u.Role == UserRole.SupportAgent, cancellationToken);

        var activeAdmins = await _dbContext.Users
            .AsNoTracking()
            .CountAsync(u => u.IsActive && u.Role == UserRole.Admin, cancellationToken);

        return new DashboardAdminMetricsDto(activeCategories, activeSupportAgents, activeAdmins);
    }

    // UpdatedAt is stored as raw DateTimeOffset text; SQLite EF Core cannot ORDER BY DateTimeOffset.
    // CreatedAt has a UTC DateTime converter (SupportRequestConfiguration) so it IS database-sortable.
    // We take the most recently created RecentCandidateLimit rows in SQL, then sort that bounded set
    // in memory by UpdatedAt. A very old request recently updated would not appear in this window;
    // the dashboard is designed for operational awareness of current activity.
    private const int RecentCandidateLimit = 100;

    private async Task<IReadOnlyList<DashboardRecentRequestDto>> GetRecentRequestsAsync(
        IQueryable<SupportRequest> baseQuery, CancellationToken cancellationToken)
    {
        var candidates = await baseQuery
            .OrderByDescending(r => r.CreatedAt)
            .Take(RecentCandidateLimit)
            .Select(r => new
            {
                r.Id,
                r.Title,
                r.Status,
                r.Priority,
                CategoryName = r.Category.Name,
                CreatedByDisplayName = r.CreatedByUser.DisplayName,
                AssignedToDisplayName = r.AssignedToUser == null ? (string?)null : r.AssignedToUser.DisplayName,
                r.UpdatedAt,
                r.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return candidates
            .OrderByDescending(r => r.UpdatedAt)
            .ThenByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Take(5)
            .Select(r => new DashboardRecentRequestDto(
                r.Id,
                r.Title,
                r.Status.ToString(),
                r.Priority.ToString(),
                r.CategoryName,
                r.CreatedByDisplayName,
                r.AssignedToDisplayName,
                r.UpdatedAt))
            .ToList();
    }

    private static IReadOnlyList<DashboardStatusCountDto> BuildStatusCounts(
        Dictionary<RequestStatus, int> dict)
    {
        return Enum.GetValues<RequestStatus>()
            .Select(s => new DashboardStatusCountDto(s.ToString(), dict.GetValueOrDefault(s, 0)))
            .ToList();
    }

    private static IReadOnlyList<DashboardPriorityCountDto> BuildPriorityCounts(
        Dictionary<RequestPriority, int> dict)
    {
        return Enum.GetValues<RequestPriority>()
            .Select(p => new DashboardPriorityCountDto(p.ToString(), dict.GetValueOrDefault(p, 0)))
            .ToList();
    }
}
