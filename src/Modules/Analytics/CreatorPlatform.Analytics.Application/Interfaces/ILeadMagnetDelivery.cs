namespace CreatorPlatform.Analytics.Application.Interfaces;

/// <summary>
/// Sends a lead page's document to the person who signed up: a personal access link (<c>products.access_grants</c>)
/// and the "your free download" email. Called inside the sign-up's transaction after the contact limit has accepted it,
/// so a rejected sign-up sends nothing. Nothing happens on a page that is not a lead page or whose product has no file.
/// A repeat sign-up of the same email on the same page gets the same link again, at most once per 24 hours; the
/// visitor sees the same answer either way, so the page never tells who is on the list.
/// </summary>
public interface ILeadMagnetDelivery
{
    Task DeliverAsync(int creatorId, int landingPageId, string normalizedEmail, DateTimeOffset now, CancellationToken ct);
}
