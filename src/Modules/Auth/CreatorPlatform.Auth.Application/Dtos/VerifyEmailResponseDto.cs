namespace CreatorPlatform.Auth.Application.Dtos;

public sealed record VerifyEmailResponseDto
{
    public string Message { get; init; } = string.Empty;

    /// <summary><see cref="VerifyEmailOutcome.Verified"/> or <see cref="VerifyEmailOutcome.Expired"/>.</summary>
    public string Outcome { get; init; } = string.Empty;

    /// <summary>Set when <see cref="Outcome"/> is Verified, so the page can greet the user.</summary>
    public string? FirstName { get; init; }

    /// <summary>Set when <see cref="Outcome"/> is Expired, so the page can offer a new link to that address.</summary>
    public string? Email { get; init; }
}

public static class VerifyEmailOutcome
{
    public const string Verified = "Verified";
    public const string Expired = "Expired";
}
