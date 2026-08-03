namespace ServiceRequest.Application.Dashboard;

public sealed record DashboardAdminMetricsDto(
    int ActiveCategories,
    int ActiveSupportAgents,
    int ActiveAdmins);
