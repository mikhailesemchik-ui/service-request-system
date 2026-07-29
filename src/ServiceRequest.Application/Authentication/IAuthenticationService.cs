namespace ServiceRequest.Application.Authentication;

public interface IAuthenticationService
{
    Task<AuthenticatedUserDto> LoginAsync(string username, string password, CancellationToken cancellationToken);
}
