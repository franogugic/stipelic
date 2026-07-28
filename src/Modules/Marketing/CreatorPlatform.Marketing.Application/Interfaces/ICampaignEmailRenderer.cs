namespace CreatorPlatform.Marketing.Application.Interfaces;

public sealed record CampaignEmailContent(string Subject, string HtmlBody, string PlainTextBody);

public interface ICampaignEmailRenderer
{
    /// <summary>Renders ONCE per campaign, not per recipient — the returned HTML/plain-text bodies
    /// contain a literal <c>{{UNSUBSCRIBE_URL}}</c> placeholder that the caller substitutes per
    /// recipient at queue time (see the send pipeline), so this (branded, potentially large) template is
    /// never rebuilt per recipient.</summary>
    CampaignEmailContent Render(
        string subject,
        string bodyText,
        string? ctaLabel,
        string? ctaUrl,
        string brandName,
        string? logoUrl,
        string primaryColor);
}
