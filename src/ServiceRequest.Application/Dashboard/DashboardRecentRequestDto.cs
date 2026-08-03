namespace ServiceRequest.Application.Dashboard;

public sealed record DashboardRecentRequestDto(
    int Id,
    string Title,
    string Status,
    string Priority,
    string CategoryName,
    string CreatedByDisplayName,
    string? AssignedToDisplayName,
    DateTimeOffset UpdatedAt);
