using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceRequest.Application.Requests;

namespace ServiceRequest.Api.Controllers;

[ApiController]
[Route("api/request-assignees")]
[Authorize(Policy = "RequireAdmin")]
public sealed class RequestAssigneesController : ControllerBase
{
    private readonly IRequestService _requestService;

    public RequestAssigneesController(IRequestService requestService)
    {
        _requestService = requestService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RequestAssigneeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<RequestAssigneeDto>>> GetAll(CancellationToken cancellationToken)
    {
        var assignees = await _requestService.GetEligibleAssigneesAsync(cancellationToken);
        return Ok(assignees);
    }
}
