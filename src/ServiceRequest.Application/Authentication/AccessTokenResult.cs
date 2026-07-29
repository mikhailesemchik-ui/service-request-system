namespace ServiceRequest.Application.Authentication;

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);
