namespace ServiceRequest.Application.Dashboard;

public sealed record DashboardSummaryDto(
    string Scope,
    int TotalRequests,
    int OpenRequests,
    int ResolvedRequests,
    int ClosedRequests,
    int CancelledRequests,
    IReadOnlyList<DashboardStatusCountDto> StatusCounts,
    IReadOnlyList<DashboardPriorityCountDto> PriorityCounts,
    DashboardStaffMetricsDto? StaffMetrics,
    DashboardAdminMetricsDto? AdminMetrics,
    IReadOnlyList<DashboardRecentRequestDto> RecentRequests);
