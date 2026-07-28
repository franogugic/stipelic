namespace CreatorPlatform.Auth.Application.Dtos;

public sealed record RequestPasswordResetResponseDto
{
    public string Message { get; init; } = string.Empty;
}
