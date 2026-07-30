using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceRequest.Application.Authentication;
using ServiceRequest.Application.Common;
using ServiceRequest.Application.Requests;

namespace ServiceRequest.Api.Controllers;

[ApiController]
[Route("api/requests")]
[Authorize]
public sealed class RequestsController : ControllerBase
{
    private readonly IRequestService _requestService;
    private readonly ICurrentUserService _currentUserService;

    public RequestsController(IRequestService requestService, ICurrentUserService currentUserService)
    {
        _requestService = requestService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<RequestListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<RequestListItemDto>>> GetList(
        [FromQuery] RequestListQuery query,
        CancellationToken cancellationToken)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync(User, cancellationToken);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var result = await _requestService.GetListAsync(query, currentUser, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{requestId}")]
    [ProducesResponseType(typeof(RequestDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestDetailsDto>> GetById(
        [Range(1, int.MaxValue, ErrorMessage = "Request id must be greater than zero.")] int requestId,
        CancellationToken cancellationToken)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync(User, cancellationToken);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var details = await _requestService.GetByIdAsync(requestId, currentUser, cancellationToken);
        return Ok(details);
    }

    [HttpPost]
    [ProducesResponseType(typeof(RequestDetailsDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestDetailsDto>> Create(
        [FromBody] CreateRequestRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync(User, cancellationToken);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var created = await _requestService.CreateAsync(request, currentUser, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { requestId = created.Id }, created);
    }

    [HttpPatch("{requestId}/assignment")]
    [Authorize(Policy = "CanManageRequests")]
    [ProducesResponseType(typeof(RequestDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestDetailsDto>> SetAssignment(
        [Range(1, int.MaxValue, ErrorMessage = "Request id must be greater than zero.")] int requestId,
        [FromBody] UpdateRequestAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync(User, cancellationToken);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var details = await _requestService.SetAssignmentAsync(requestId, request, currentUser, cancellationToken);
        return Ok(details);
    }

    [HttpPatch("{requestId}/status")]
    [ProducesResponseType(typeof(RequestDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestDetailsDto>> ChangeStatus(
        [Range(1, int.MaxValue, ErrorMessage = "Request id must be greater than zero.")] int requestId,
        [FromBody] UpdateRequestStatusRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync(User, cancellationToken);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var details = await _requestService.ChangeStatusAsync(requestId, request, currentUser, cancellationToken);
        return Ok(details);
    }

    [HttpGet("{requestId}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<RequestHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RequestHistoryDto>>> GetHistory(
        [Range(1, int.MaxValue, ErrorMessage = "Request id must be greater than zero.")] int requestId,
        CancellationToken cancellationToken)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync(User, cancellationToken);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var history = await _requestService.GetHistoryAsync(requestId, currentUser, cancellationToken);
        return Ok(history);
    }
}
