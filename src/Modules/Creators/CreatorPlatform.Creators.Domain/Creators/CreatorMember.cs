namespace CreatorPlatform.Creators.Domain.Creators;

public sealed class CreatorMember
{
    private CreatorMember()
    {
    }

    private CreatorMember(
        Guid publicId,
        Creator creator,
        int userId,
        CreatorMemberRole role,
        CreatorMemberStatus status,
        DateTimeOffset? invitedAt,
        DateTimeOffset? joinedAt,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        Creator = creator;
        UserId = userId;
        Role = role;
        Status = status;
        InvitedAt = invitedAt;
        JoinedAt = joinedAt;
        CreatedAt = createdAt;
    }

    public static CreatorMember CreateOwner(Creator creator, int userId, DateTimeOffset createdAt)
    {
        return new CreatorMember(
            Guid.NewGuid(),
            creator,
            userId,
            CreatorMemberRole.Owner,
            CreatorMemberStatus.Active,
            null,
            createdAt,
            createdAt);
    }

    public static CreatorMember Invite(
        Creator creator,
        int userId,
        CreatorMemberRole role,
        DateTimeOffset invitedAt)
    {
        return new CreatorMember(
            Guid.NewGuid(),
            creator,
            userId,
            role,
            CreatorMemberStatus.Invited,
            invitedAt,
            null,
            invitedAt);
    }

    public void AcceptInvitation(DateTimeOffset joinedAt)
    {
        Status = CreatorMemberStatus.Active;
        JoinedAt = joinedAt;
    }

    public void ChangeRole(CreatorMemberRole role)
    {
        Role = role;
    }

    public void Remove()
    {
        Status = CreatorMemberStatus.Removed;
    }

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public int CreatorId { get; private set; }

    public Creator Creator { get; private set; } = null!;

    public int UserId { get; private set; }

    public CreatorMemberRole Role { get; private set; }

    public CreatorMemberStatus Status { get; private set; }

    public DateTimeOffset? InvitedAt { get; private set; }

    public DateTimeOffset? JoinedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
