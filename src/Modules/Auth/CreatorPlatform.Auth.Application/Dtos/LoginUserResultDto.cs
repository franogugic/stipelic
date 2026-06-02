namespace CreatorPlatform.Auth.Application.Dtos;

public sealed record LoginUserResultDto
{
    public string SessionToken { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public LoginUserResponseDto User { get; init; } = new();
}
