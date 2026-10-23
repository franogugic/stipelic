namespace CreatorPlatform.Auth.Application.Dtos;

public sealed record VerifyEmailResponseDto
{
    public string Message { get; init; } = string.Empty;

    /// <summary><see cref="VerifyEmailOutcome.Verified"/>. A link that cannot be used answers 400 with a code instead
    /// (VERIFICATION_LINK_REPLACED / _USED / _EXPIRED).</summary>
    public string Outcome { get; init; } = string.Empty;

    /// <summary>Set when <see cref="Outcome"/> is Verified, so the page can greet the user.</summary>
    public string? FirstName { get; init; }
}

public static class VerifyEmailOutcome
{
    public const string Verified = "Verified";
}
