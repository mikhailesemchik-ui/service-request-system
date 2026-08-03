using ServiceRequest.Application.Authentication;

namespace ServiceRequest.Application.Dashboard;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(
        AuthenticatedUserDto currentUser,
        CancellationToken cancellationToken);
}
