namespace CreatorPlatform.Marketing.Application.Interfaces;

public interface IClickTrackingService
{
    /// <summary>The destination of a tracked link, read from the database (never from the request), with the click
    /// recorded; null for an invalid token or a link that is not the recipient's (the caller sends them to the
    /// not-found page). An invalid token costs no database query. A failure to record is logged and does not stop
    /// the redirect.</summary>
    Task<string?> ResolveClickAsync(string token, CancellationToken ct);
}
