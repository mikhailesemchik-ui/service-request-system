using ServiceRequest.Domain.Enums;

namespace ServiceRequest.Application.Requests;

public sealed record RequestAssigneeDto(int Id, string DisplayName, UserRole Role);
