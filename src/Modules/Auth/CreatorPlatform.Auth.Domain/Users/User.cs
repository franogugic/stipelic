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

    public void VerifyEmail(DateTimeOffset verifiedAt)
    {
        EmailVerifiedAt = verifiedAt;
        Status = UserStatus.Active;
        UpdatedAt = verifiedAt;
    }

    /// <summary>Records when the user agreed to the Terms and Privacy Policy (set once, at registration).</summary>
    public void AcceptTerms(DateTimeOffset acceptedAt)
    {
        TermsAcceptedAt = acceptedAt;
        UpdatedAt = acceptedAt;
    }

    /// <summary>Replaces the display name. Values arrive already trimmed and validated (same rules as
    /// registration).</summary>
    public void UpdateName(string firstName, string lastName, DateTimeOffset updatedAt)
    {
        FirstName = firstName;
        LastName = lastName;
        UpdatedAt = updatedAt;
    }

    /// <summary>Switches the sign-in email to an address the user just proved they control (confirmation link),
    /// so it counts as verified. A user still waiting for their first verification becomes Active; a Disabled
    /// user stays Disabled.</summary>
    public void ChangeEmail(string newEmail, DateTimeOffset changedAt)
    {
        Email = newEmail;
        EmailVerifiedAt = changedAt;
        if (Status == UserStatus.PendingEmailVerification)
            Status = UserStatus.Active;
        UpdatedAt = changedAt;
    }

    public void SetPassword(string newPasswordHash, DateTimeOffset updatedAt)
    {
        PasswordHash = newPasswordHash;
        UpdatedAt = updatedAt;
    }

    public bool IsEmailVerified => EmailVerifiedAt is not null && Status == UserStatus.Active;

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public DateTimeOffset? EmailVerifiedAt { get; private set; }

    /// <summary>NULL for users who registered before terms acceptance was recorded.</summary>
    public DateTimeOffset? TermsAcceptedAt { get; private set; }

    public UserStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
