namespace ServiceRequest.Application.Dashboard;

public sealed record DashboardStaffMetricsDto(
    int UnassignedActiveRequests,
    int AssignedToMe,
    int ActiveAssignedToMe);
