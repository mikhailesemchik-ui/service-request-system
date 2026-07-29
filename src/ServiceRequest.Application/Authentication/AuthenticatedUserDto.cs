namespace ServiceRequest.Application.Authentication;

public sealed record AuthenticatedUserDto(
    int Id,
    string Username,
    string DisplayName,
    string Email,
    string Role);
