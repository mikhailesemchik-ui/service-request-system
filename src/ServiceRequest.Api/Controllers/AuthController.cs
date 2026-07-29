using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceRequest.Application.Authentication;

namespace ServiceRequest.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ITokenService _tokenService;
    private readonly ICurrentUserService _currentUserService;

    public AuthController(
        IAuthenticationService authenticationService,
        ITokenService tokenService,
        ICurrentUserService currentUserService)
    {
        _authenticationService = authenticationService;
        _tokenService = tokenService;
        _currentUserService = currentUserService;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _authenticationService.LoginAsync(request.Username, request.Password, cancellationToken);
        var token = _tokenService.CreateToken(user);

        return Ok(new LoginResponse(token.Token, "Bearer", token.ExpiresAt, user));
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(AuthenticatedUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticatedUserDto>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var user = await _currentUserService.GetCurrentUserAsync(User, cancellationToken);

        return user is null ? Unauthorized() : Ok(user);
    }
}
