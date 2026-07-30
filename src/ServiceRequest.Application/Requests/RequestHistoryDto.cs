namespace ServiceRequest.Application.Requests;

public sealed record RequestHistoryDto(
    int Id,
    string Action,
    string? PreviousValue,
    string? NewValue,
    string? PreviousDisplayValue,
    string? NewDisplayValue,
    RequestUserSummaryDto ChangedBy,
    DateTimeOffset CreatedAt);
