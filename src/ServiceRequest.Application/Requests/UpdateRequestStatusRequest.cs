using ServiceRequest.Domain.Enums;

namespace ServiceRequest.Application.Requests;

public sealed class UpdateRequestStatusRequest
{
    public RequestStatus Status { get; init; }
}
