namespace CreatorPlatform.Analytics.Domain.EmailCaptures;

public sealed class EmailCapture
{
    public Guid Id { get; init; }

    public int LandingPageId { get; init; }

    public int? ProductId { get; init; }

    public string Email { get; init; } = string.Empty;

    public DateTimeOffset CapturedAt { get; init; }
}
