namespace CreatorPlatform.Auth.Application.Dtos;

public sealed record VerifyEmailResponseDto
{
    public string Message { get; init; } = string.Empty;
}
