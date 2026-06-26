namespace CreatorPlatform.Analytics.Application.Dtos;

public sealed class EmailCaptureResponseDto
{
    public required string Email { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
}
