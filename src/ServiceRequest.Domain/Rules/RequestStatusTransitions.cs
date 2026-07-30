using ServiceRequest.Domain.Enums;

namespace ServiceRequest.Domain.Rules;

public static class RequestStatusTransitions
{
    private static readonly IReadOnlyDictionary<RequestStatus, RequestStatus[]>
        AllowedTransitions =
            new Dictionary<RequestStatus, RequestStatus[]>
            {
                [RequestStatus.New] =
                [
                    RequestStatus.InProgress,
                    RequestStatus.Cancelled,
                ],
                [RequestStatus.InProgress] =
                [
                    RequestStatus.WaitingForUser,
                    RequestStatus.Resolved,
                    RequestStatus.Cancelled,
                ],
                [RequestStatus.WaitingForUser] =
                [
                    RequestStatus.InProgress,
                    RequestStatus.Resolved,
                    RequestStatus.Cancelled,
                ],
                [RequestStatus.Resolved] =
                [
                    RequestStatus.InProgress,
                    RequestStatus.Closed,
                ],
                [RequestStatus.Closed] = [],
                [RequestStatus.Cancelled] = [],
            };

    public static bool CanTransition(RequestStatus from, RequestStatus to) =>
        AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
}
