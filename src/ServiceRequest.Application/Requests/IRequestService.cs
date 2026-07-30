using ServiceRequest.Application.Authentication;
using ServiceRequest.Application.Common;

namespace ServiceRequest.Application.Requests;

public interface IRequestService
{
    Task<PagedResult<RequestListItemDto>> GetListAsync(
        RequestListQuery query,
        AuthenticatedUserDto currentUser,
        CancellationToken cancellationToken);

    Task<RequestDetailsDto> GetByIdAsync(
        int requestId,
        AuthenticatedUserDto currentUser,
        CancellationToken cancellationToken);

    Task<RequestDetailsDto> CreateAsync(
        CreateRequestRequest request,
        AuthenticatedUserDto currentUser,
        CancellationToken cancellationToken);

    Task<RequestDetailsDto> SetAssignmentAsync(
        int requestId,
        UpdateRequestAssignmentRequest request,
        AuthenticatedUserDto currentUser,
        CancellationToken cancellationToken);

    Task<RequestDetailsDto> ChangeStatusAsync(
        int requestId,
        UpdateRequestStatusRequest request,
        AuthenticatedUserDto currentUser,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RequestHistoryDto>> GetHistoryAsync(
        int requestId,
        AuthenticatedUserDto currentUser,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RequestAssigneeDto>> GetEligibleAssigneesAsync(CancellationToken cancellationToken);
}
