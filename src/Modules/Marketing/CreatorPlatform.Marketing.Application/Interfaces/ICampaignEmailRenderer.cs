namespace CreatorPlatform.Marketing.Application.Interfaces;

/// <summary>Why the recipient is on the list, as the footer says it: they signed up (a page or the whole list), or they
/// bought (the buyers of a landing page — marketing to existing customers).</summary>
public enum CampaignAudienceReason
{
    SignedUp,
    Bought
}

public sealed record CampaignEmailContent(string Subject, string HtmlBody, string PlainTextBody);

public interface ICampaignEmailRenderer
{
    /// <summary>Renders ONCE per campaign, not per recipient — the returned HTML/plain-text bodies
    /// contain a literal <c>{{UNSUBSCRIBE_URL}}</c> placeholder (and the HTML one a literal
    /// <c>{{OPEN_PIXEL_URL}}</c> placeholder) that the caller substitutes per recipient at queue time (see
    /// the send pipeline), so this (branded, potentially large) template is never rebuilt per
    /// recipient. <paramref name="supportEmail"/> is the public address named in the footer ("Reply to …").
    /// <paramref name="reason"/> picks the footer's line: "…because you signed up at {brand}" or "…because you bought from {brand}".</summary>
    CampaignEmailContent Render(
        string subject,
        string bodyText,
        string? ctaLabel,
        string? ctaUrl,
        string brandName,
        string? logoUrl,
        string primaryColor,
        string? supportEmail = null,
        CampaignAudienceReason reason = CampaignAudienceReason.SignedUp);
}
