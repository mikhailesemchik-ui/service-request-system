namespace ServiceRequest.Application.Requests;

public sealed record RequestCommentDto(
    int Id,
    string Content,
    bool IsInternal,
    RequestCommentAuthorDto Author,
    DateTimeOffset CreatedAt);
