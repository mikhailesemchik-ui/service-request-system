using ServiceRequest.Domain.Enums;

namespace ServiceRequest.Application.Requests;

public sealed record RequestDetailsDto(
    int Id,
    string Title,
    string Description,
    RequestStatus Status,
    RequestPriority Priority,
    RequestCategorySummaryDto Category,
    RequestUserSummaryDto CreatedBy,
    RequestUserSummaryDto? AssignedTo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? CancelledAt);
