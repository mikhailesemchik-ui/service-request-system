using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;
using ServiceRequest.Domain.Exceptions;

namespace ServiceRequest.UnitTests.Domain.Entities;

public class SupportRequestTests
{
    private static RequestCategory CreateCategory() => new("Hardware", "Hardware-related requests");

    private static ApplicationUser CreateUser() =>
        new("jane.doe", "Jane Doe", "jane.doe@example.com", UserRole.Employee);

    private static ApplicationUser CreateAgent(string username = "agent.smith") =>
        new(username, "Agent Smith", $"{username}@example.com", UserRole.SupportAgent);

    private static ApplicationUser CreateAdmin() =>
        new("root.admin", "Root Admin", "root.admin@example.com", UserRole.Admin);

    private static SupportRequest CreateRequest() =>
        new("Printer not working", "The office printer jams every time it is used.", RequestPriority.Medium, CreateCategory(), CreateUser());

    /// <summary>Drives a fresh request through valid transitions to reach <paramref name="status"/>, assigning an agent along the way.</summary>
    private static SupportRequest CreateRequestInStatus(RequestStatus status, ApplicationUser? agent = null)
    {
        var request = CreateRequest();
        request.AssignTo(agent ?? CreateAgent());

        if (status == RequestStatus.New)
        {
            return request;
        }

        if (status == RequestStatus.Cancelled)
        {
            request.ChangeStatus(RequestStatus.Cancelled);
            return request;
        }

        request.ChangeStatus(RequestStatus.InProgress);

        if (status == RequestStatus.InProgress)
        {
            return request;
        }

        if (status == RequestStatus.WaitingForUser)
        {
            request.ChangeStatus(RequestStatus.WaitingForUser);
            return request;
        }

        request.ChangeStatus(RequestStatus.Resolved);

        if (status == RequestStatus.Resolved)
        {
            return request;
        }

        request.ChangeStatus(RequestStatus.Closed);
        return request;
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenTitleIsBlank_ThrowsArgumentException(string title)
    {
        Assert.Throws<ArgumentException>(() =>
            new SupportRequest(title, "Description", RequestPriority.Medium, CreateCategory(), CreateUser()));
    }

    [Fact]
    public void Constructor_WhenTitleIsNull_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new SupportRequest(null!, "Description", RequestPriority.Medium, CreateCategory(), CreateUser()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenDescriptionIsBlank_ThrowsArgumentException(string description)
    {
        Assert.Throws<ArgumentException>(() =>
            new SupportRequest("Title", description, RequestPriority.Medium, CreateCategory(), CreateUser()));
    }

    [Fact]
    public void Constructor_WhenDescriptionIsNull_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new SupportRequest("Title", null!, RequestPriority.Medium, CreateCategory(), CreateUser()));
    }

    [Fact]
    public void Constructor_WhenCategoryIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SupportRequest("Title", "Description", RequestPriority.Medium, null!, CreateUser()));
    }

    [Fact]
    public void Constructor_WhenCreatedByUserIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SupportRequest("Title", "Description", RequestPriority.Medium, CreateCategory(), null!));
    }

    [Fact]
    public void Constructor_WhenValuesAreValid_RetainsSuppliedValues()
    {
        var category = CreateCategory();
        var creator = CreateUser();

        var request = new SupportRequest("Printer not working", "The office printer jams.", RequestPriority.High, category, creator);

        Assert.Equal("Printer not working", request.Title);
        Assert.Equal("The office printer jams.", request.Description);
        Assert.Equal(RequestPriority.High, request.Priority);
        Assert.Equal(RequestStatus.New, request.Status);
        Assert.Same(category, request.Category);
        Assert.Same(creator, request.CreatedByUser);
        Assert.Null(request.AssignedToUser);
    }

    [Fact]
    public void Constructor_TrimsTitle()
    {
        var request = new SupportRequest("  Printer not working  ", "The office printer jams.", RequestPriority.High, CreateCategory(), CreateUser());

        Assert.Equal("Printer not working", request.Title);
    }

    [Fact]
    public void Constructor_TrimsDescription()
    {
        var request = new SupportRequest("Printer not working", "  The office printer jams.  ", RequestPriority.High, CreateCategory(), CreateUser());

        Assert.Equal("The office printer jams.", request.Description);
    }

    [Fact]
    public void Constructor_NewRequest_HasNoAssignee()
    {
        var request = new SupportRequest("Printer not working", "The office printer jams.", RequestPriority.High, CreateCategory(), CreateUser());

        Assert.Null(request.AssignedToUser);
        Assert.Null(request.AssignedToUserId);
    }

    [Fact]
    public void Constructor_NewRequest_HasNullLifecycleTimestamps()
    {
        var request = new SupportRequest("Printer not working", "The office printer jams.", RequestPriority.High, CreateCategory(), CreateUser());

        Assert.Null(request.ResolvedAt);
        Assert.Null(request.ClosedAt);
        Assert.Null(request.CancelledAt);
    }

    [Fact]
    public void Constructor_NewRequest_SetsCreatedAtAndUpdatedAtToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;

        var request = new SupportRequest("Printer not working", "The office printer jams.", RequestPriority.High, CreateCategory(), CreateUser());

        var after = DateTimeOffset.UtcNow;

        Assert.InRange(request.CreatedAt, before, after);
        Assert.Equal(request.CreatedAt, request.UpdatedAt);
    }

    // Assignment

    [Fact]
    public void AssignTo_WhenNull_ThrowsArgumentNullException()
    {
        var request = CreateRequest();

        Assert.Throws<ArgumentNullException>(() => request.AssignTo(null!));
    }

    [Fact]
    public void AssignTo_UnassignedRequest_AssignsAndReturnsTrue()
    {
        var request = CreateRequest();
        var agent = CreateAgent();

        var changed = request.AssignTo(agent);

        Assert.True(changed);
        Assert.Same(agent, request.AssignedToUser);
        Assert.Equal(agent.Id, request.AssignedToUserId);
    }

    // Reassigning to a genuinely different agent requires two distinct, persisted user IDs
    // (every unsaved ApplicationUser in a pure domain test defaults to Id 0), so that scenario
    // is covered at the integration level instead (RequestServiceTests: Admin reassigns).

    [Fact]
    public void AssignTo_ToAdmin_Succeeds()
    {
        var request = CreateRequest();
        var admin = CreateAdmin();

        var changed = request.AssignTo(admin);

        Assert.True(changed);
        Assert.Equal(admin.Id, request.AssignedToUserId);
    }

    [Fact]
    public void AssignTo_SameAgentAgain_IsNoOp()
    {
        var request = CreateRequest();
        var agent = CreateAgent();
        request.AssignTo(agent);

        var changed = request.AssignTo(agent);

        Assert.False(changed);
    }

    [Fact]
    public void AssignTo_WhenAssigneeIsEmployee_ThrowsInvalidRequestAssigneeException()
    {
        var request = CreateRequest();

        Assert.Throws<InvalidRequestAssigneeException>(() => request.AssignTo(CreateUser()));
    }

    [Fact]
    public void AssignTo_WhenAssigneeIsInactive_ThrowsInvalidRequestAssigneeException()
    {
        var request = CreateRequest();
        var agent = CreateAgent();
        agent.SetActiveState(false);

        Assert.Throws<InvalidRequestAssigneeException>(() => request.AssignTo(agent));
    }

    [Fact]
    public void AssignTo_WhenRequestIsClosed_ThrowsInvalidRequestAssigneeException()
    {
        var request = CreateRequestInStatus(RequestStatus.Closed);

        Assert.Throws<InvalidRequestAssigneeException>(() => request.AssignTo(CreateAgent("agent.two")));
    }

    [Fact]
    public void AssignTo_WhenRequestIsCancelled_ThrowsInvalidRequestAssigneeException()
    {
        var request = CreateRequestInStatus(RequestStatus.Cancelled);

        Assert.Throws<InvalidRequestAssigneeException>(() => request.AssignTo(CreateAgent()));
    }

    [Fact]
    public void RemoveAssignment_WhenAssigned_RemovesAndReturnsTrue()
    {
        var request = CreateRequest();
        request.AssignTo(CreateAgent());

        var changed = request.RemoveAssignment();

        Assert.True(changed);
        Assert.Null(request.AssignedToUser);
        Assert.Null(request.AssignedToUserId);
    }

    [Fact]
    public void RemoveAssignment_WhenAlreadyUnassigned_IsNoOp()
    {
        var request = CreateRequest();

        var changed = request.RemoveAssignment();

        Assert.False(changed);
    }

    [Fact]
    public void RemoveAssignment_WhenRequestIsClosed_ThrowsInvalidRequestAssigneeException()
    {
        var request = CreateRequestInStatus(RequestStatus.Closed);

        Assert.Throws<InvalidRequestAssigneeException>(() => request.RemoveAssignment());
    }

    // Status transitions

    private static readonly HashSet<(RequestStatus From, RequestStatus To)> ValidTransitionSet = new()
    {
        (RequestStatus.New, RequestStatus.InProgress),
        (RequestStatus.New, RequestStatus.Cancelled),
        (RequestStatus.InProgress, RequestStatus.WaitingForUser),
        (RequestStatus.InProgress, RequestStatus.Resolved),
        (RequestStatus.InProgress, RequestStatus.Cancelled),
        (RequestStatus.WaitingForUser, RequestStatus.InProgress),
        (RequestStatus.WaitingForUser, RequestStatus.Resolved),
        (RequestStatus.WaitingForUser, RequestStatus.Cancelled),
        (RequestStatus.Resolved, RequestStatus.InProgress),
        (RequestStatus.Resolved, RequestStatus.Closed),
    };

    public static IEnumerable<object[]> ValidTransitions() =>
        ValidTransitionSet.Select(pair => new object[] { pair.From, pair.To });

    public static IEnumerable<object[]> InvalidTransitions()
    {
        var statuses = Enum.GetValues<RequestStatus>();

        foreach (var from in statuses)
        {
            foreach (var to in statuses)
            {
                if (from == to || ValidTransitionSet.Contains((from, to)))
                {
                    continue;
                }

                yield return new object[] { from, to };
            }
        }
    }

    [Theory]
    [MemberData(nameof(ValidTransitions))]
    public void ChangeStatus_ValidTransition_Succeeds(RequestStatus from, RequestStatus to)
    {
        var request = CreateRequestInStatus(from);

        var changed = request.ChangeStatus(to);

        Assert.True(changed);
        Assert.Equal(to, request.Status);
    }

    [Theory]
    [MemberData(nameof(InvalidTransitions))]
    public void ChangeStatus_InvalidTransition_ThrowsInvalidRequestStatusTransitionException(RequestStatus from, RequestStatus to)
    {
        var request = CreateRequestInStatus(from);

        Assert.Throws<InvalidRequestStatusTransitionException>(() => request.ChangeStatus(to));
    }

    [Fact]
    public void ChangeStatus_SameStatus_IsNoOp()
    {
        var request = CreateRequestInStatus(RequestStatus.InProgress);

        var changed = request.ChangeStatus(RequestStatus.InProgress);

        Assert.False(changed);
    }

    [Fact]
    public void ChangeStatus_ToInProgressWithoutAssignee_ThrowsInvalidRequestStatusTransitionException()
    {
        var request = CreateRequest();

        Assert.Throws<InvalidRequestStatusTransitionException>(() => request.ChangeStatus(RequestStatus.InProgress));
    }

    [Fact]
    public void ChangeStatus_ToResolved_SetsResolvedAtAndClearsClosedAndCancelled()
    {
        var request = CreateRequestInStatus(RequestStatus.InProgress);

        request.ChangeStatus(RequestStatus.Resolved);

        Assert.NotNull(request.ResolvedAt);
        Assert.Null(request.ClosedAt);
        Assert.Null(request.CancelledAt);
    }

    [Fact]
    public void ChangeStatus_ToClosed_SetsClosedAtAndKeepsResolvedAt()
    {
        var request = CreateRequestInStatus(RequestStatus.Resolved);
        var resolvedAt = request.ResolvedAt;

        request.ChangeStatus(RequestStatus.Closed);

        Assert.NotNull(request.ClosedAt);
        Assert.Equal(resolvedAt, request.ResolvedAt);
    }

    [Fact]
    public void ChangeStatus_ToCancelled_SetsCancelledAtAndClearsResolvedAndClosed()
    {
        var request = CreateRequestInStatus(RequestStatus.InProgress);

        request.ChangeStatus(RequestStatus.Cancelled);

        Assert.NotNull(request.CancelledAt);
        Assert.Null(request.ResolvedAt);
        Assert.Null(request.ClosedAt);
    }

    [Fact]
    public void ChangeStatus_ReopenFromResolvedToInProgress_ClearsResolvedAt()
    {
        var request = CreateRequestInStatus(RequestStatus.Resolved);

        request.ChangeStatus(RequestStatus.InProgress);

        Assert.Null(request.ResolvedAt);
        Assert.Null(request.ClosedAt);
        Assert.Null(request.CancelledAt);
    }

    [Fact]
    public void ChangeStatus_NoOp_DoesNotChangeUpdatedAt()
    {
        var request = CreateRequestInStatus(RequestStatus.InProgress);
        var updatedAtBefore = request.UpdatedAt;

        request.ChangeStatus(RequestStatus.InProgress);

        Assert.Equal(updatedAtBefore, request.UpdatedAt);
    }

    [Fact]
    public void ChangeStatus_ActualMutation_ChangesUpdatedAt()
    {
        var request = CreateRequestInStatus(RequestStatus.InProgress);
        var updatedAtBefore = request.UpdatedAt;

        request.ChangeStatus(RequestStatus.Resolved);

        Assert.True(request.UpdatedAt > updatedAtBefore);
    }

    [Fact]
    public void AssignTo_NoOp_DoesNotChangeUpdatedAt()
    {
        var request = CreateRequest();
        var agent = CreateAgent();
        request.AssignTo(agent);
        var updatedAtBefore = request.UpdatedAt;

        request.AssignTo(agent);

        Assert.Equal(updatedAtBefore, request.UpdatedAt);
    }

    [Fact]
    public void AssignTo_ActualMutation_ChangesUpdatedAt()
    {
        var request = CreateRequest();
        var updatedAtBefore = request.UpdatedAt;

        request.AssignTo(CreateAgent());

        Assert.True(request.UpdatedAt > updatedAtBefore);
    }

    // ChangeCategory

    [Fact]
    public void ChangeCategory_WhenNull_ThrowsArgumentNullException()
    {
        var request = CreateRequest();

        Assert.Throws<ArgumentNullException>(() => request.ChangeCategory(null!));
    }

    // Changing to a genuinely different category requires two distinct, persisted category IDs
    // (every unsaved RequestCategory in a pure domain test defaults to Id 0), so that scenario
    // is covered at the integration level instead (RequestClassificationServiceTests).

    [Fact]
    public void ChangeCategory_SameCategory_IsNoOp()
    {
        var original = CreateCategory();
        var request = new SupportRequest("Title", "Description", RequestPriority.Medium, original, CreateUser());

        var changed = request.ChangeCategory(original);

        Assert.False(changed);
        Assert.Same(original, request.Category);
    }

    [Fact]
    public void ChangeCategory_WhenClosed_ThrowsRequestClassificationLockedException()
    {
        var request = CreateRequestInStatus(RequestStatus.Closed);

        Assert.Throws<RequestClassificationLockedException>(() => request.ChangeCategory(new RequestCategory("Other", "Other")));
    }

    [Fact]
    public void ChangeCategory_WhenCancelled_ThrowsRequestClassificationLockedException()
    {
        var request = CreateRequestInStatus(RequestStatus.Cancelled);

        Assert.Throws<RequestClassificationLockedException>(() => request.ChangeCategory(new RequestCategory("Other", "Other")));
    }

    [Fact]
    public void ChangeCategory_NoOp_DoesNotChangeUpdatedAt()
    {
        var original = CreateCategory();
        var request = new SupportRequest("Title", "Description", RequestPriority.Medium, original, CreateUser());
        var updatedAtBefore = request.UpdatedAt;

        request.ChangeCategory(original);

        Assert.Equal(updatedAtBefore, request.UpdatedAt);
    }

    // ChangePriority

    [Fact]
    public void ChangePriority_DifferentPriority_UpdatesPriorityAndReturnsTrue()
    {
        var request = new SupportRequest("Title", "Description", RequestPriority.Low, CreateCategory(), CreateUser());

        var changed = request.ChangePriority(RequestPriority.High);

        Assert.True(changed);
        Assert.Equal(RequestPriority.High, request.Priority);
    }

    [Fact]
    public void ChangePriority_SamePriority_IsNoOp()
    {
        var request = new SupportRequest("Title", "Description", RequestPriority.Medium, CreateCategory(), CreateUser());

        var changed = request.ChangePriority(RequestPriority.Medium);

        Assert.False(changed);
        Assert.Equal(RequestPriority.Medium, request.Priority);
    }

    [Fact]
    public void ChangePriority_WhenClosed_ThrowsRequestClassificationLockedException()
    {
        var request = CreateRequestInStatus(RequestStatus.Closed);

        Assert.Throws<RequestClassificationLockedException>(() => request.ChangePriority(RequestPriority.High));
    }

    [Fact]
    public void ChangePriority_WhenCancelled_ThrowsRequestClassificationLockedException()
    {
        var request = CreateRequestInStatus(RequestStatus.Cancelled);

        Assert.Throws<RequestClassificationLockedException>(() => request.ChangePriority(RequestPriority.High));
    }

    [Fact]
    public void ChangePriority_ActualMutation_ChangesUpdatedAt()
    {
        var request = new SupportRequest("Title", "Description", RequestPriority.Low, CreateCategory(), CreateUser());
        var updatedAtBefore = request.UpdatedAt;

        request.ChangePriority(RequestPriority.High);

        Assert.True(request.UpdatedAt > updatedAtBefore);
    }

    [Fact]
    public void ChangePriority_NoOp_DoesNotChangeUpdatedAt()
    {
        var request = new SupportRequest("Title", "Description", RequestPriority.Medium, CreateCategory(), CreateUser());
        var updatedAtBefore = request.UpdatedAt;

        request.ChangePriority(RequestPriority.Medium);

        Assert.Equal(updatedAtBefore, request.UpdatedAt);
    }
}
