namespace CreatorPlatform.Auth.Domain.Users;

public sealed class User
{
    private User()
    {
    }

    private User(
        Guid publicId,
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        UserStatus status,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        Email = email;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static User Create(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        DateTimeOffset createdAt)
    {
        return new User(
            Guid.NewGuid(),
            email,
            passwordHash,
            firstName,
            lastName,
            UserStatus.PendingEmailVerification,
            createdAt);
    }

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public DateTimeOffset? EmailVerifiedAt { get; private set; }

    public UserStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
