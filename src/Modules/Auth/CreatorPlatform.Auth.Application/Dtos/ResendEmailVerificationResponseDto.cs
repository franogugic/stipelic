namespace CreatorPlatform.Auth.Application.Dtos;

public sealed record ResendEmailVerificationResponseDto
{
    public string Message { get; init; } = string.Empty;
}
