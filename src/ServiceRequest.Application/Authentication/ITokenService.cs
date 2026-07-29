namespace ServiceRequest.Application.Authentication;

public interface ITokenService
{
    AccessTokenResult CreateToken(AuthenticatedUserDto user);
}
