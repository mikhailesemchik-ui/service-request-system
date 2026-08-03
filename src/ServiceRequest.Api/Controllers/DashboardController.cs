using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceRequest.Application.Authentication;
using ServiceRequest.Application.Dashboard;

namespace ServiceRequest.Api.Controllers;

/// <summary>
/// Returns a role-aware dashboard summary for the authenticated user.
/// Employees see their own request statistics; SupportAgents see operational metrics
/// across all requests; Admins additionally see category and staff counts.
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly ICurrentUserService _currentUserService;

    public DashboardController(IDashboardService dashboardService, ICurrentUserService currentUserService)
    {
        _dashboardService = dashboardService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Returns a role-aware dashboard summary. Employee scope covers only the current user's
    /// own requests. SupportAgent and Admin scopes cover all requests.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync(User, cancellationToken);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var summary = await _dashboardService.GetSummaryAsync(currentUser, cancellationToken);
        return Ok(summary);
    }
}
