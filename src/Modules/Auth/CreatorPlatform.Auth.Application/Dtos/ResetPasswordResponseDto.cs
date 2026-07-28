namespace CreatorPlatform.Auth.Application.Dtos;

public sealed record ResetPasswordResponseDto
{
    public string Message { get; init; } = string.Empty;
}
