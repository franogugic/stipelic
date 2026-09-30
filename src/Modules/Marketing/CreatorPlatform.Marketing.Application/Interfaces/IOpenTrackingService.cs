namespace CreatorPlatform.Marketing.Application.Interfaces;

public interface IOpenTrackingService
{
    /// <summary>Records the first open for the recipient the token was minted for. Never throws for bad
    /// input or a failing database — this backs a public tracking pixel that must always answer with the
    /// image, so an invalid token is ignored silently and a storage error is only logged.</summary>
    Task RecordOpenAsync(string token, CancellationToken ct);
}
