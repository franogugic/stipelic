namespace CreatorPlatform.Auth.Application.Dtos;

public sealed record LogoutResponseDto
{
    public string Message { get; init; } = string.Empty;
}
