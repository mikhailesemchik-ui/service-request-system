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
}
