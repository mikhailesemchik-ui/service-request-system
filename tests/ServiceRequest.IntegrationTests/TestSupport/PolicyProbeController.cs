using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ServiceRequest.IntegrationTests.TestSupport;

[ApiController]
[Route("api/test/policy-probe")]
public sealed class PolicyProbeController : ControllerBase
{
    [HttpGet("employee")]
    [Authorize(Policy = "RequireEmployee")]
    public IActionResult Employee() => Ok();

    [HttpGet("support-agent")]
    [Authorize(Policy = "RequireSupportAgent")]
    public IActionResult SupportAgent() => Ok();

    [HttpGet("admin")]
    [Authorize(Policy = "RequireAdmin")]
    public IActionResult Admin() => Ok();

    [HttpGet("manage-requests")]
    [Authorize(Policy = "CanManageRequests")]
    public IActionResult ManageRequests() => Ok();
}
