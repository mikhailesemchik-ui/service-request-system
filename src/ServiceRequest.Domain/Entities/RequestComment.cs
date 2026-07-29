namespace ServiceRequest.Domain.Entities;

public sealed class RequestComment
{
    public int Id { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public bool IsInternal { get; private set; }

    public int ServiceRequestId { get; private set; }

    public SupportRequest SupportRequest { get; private set; } = null!;

    public int AuthorUserId { get; private set; }

    public ApplicationUser AuthorUser { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    private RequestComment()
    {
    }

    public RequestComment(string content, bool isInternal, SupportRequest supportRequest, ApplicationUser authorUser)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Comment content cannot be blank.", nameof(content));
        }

        ArgumentNullException.ThrowIfNull(supportRequest);
        ArgumentNullException.ThrowIfNull(authorUser);

        Content = content;
        IsInternal = isInternal;
        SupportRequest = supportRequest;
        ServiceRequestId = supportRequest.Id;
        AuthorUser = authorUser;
        AuthorUserId = authorUser.Id;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
