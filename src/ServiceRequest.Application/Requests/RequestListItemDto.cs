using ServiceRequest.Domain.Enums;

namespace ServiceRequest.Application.Requests;

public sealed record RequestListItemDto(
    int Id,
    string Title,
    RequestStatus Status,
    RequestPriority Priority,
    RequestCategorySummaryDto Category,
    RequestUserSummaryDto CreatedBy,
    RequestUserSummaryDto? AssignedTo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
