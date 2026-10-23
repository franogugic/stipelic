using CreatorPlatform.Marketing.Domain.Campaigns;

namespace CreatorPlatform.Marketing.Application.Interfaces;

/// <summary>A link of the rendered email: where it points, and the placeholder that stands for its URL in both
/// bodies until the send pipeline puts in each recipient's tracked URL. The placeholder carries a per-render
/// random part, so text a creator types can never collide with it.</summary>
public sealed record RenderedLink(int Position, string Url, CampaignLinkKind Kind, string? Label, string Placeholder);

/// <summary><see cref="Links"/>: the distinct links in order of first appearance (CTA button and body URLs).</summary>
public sealed record CampaignEmailContent(string Subject, string HtmlBody, string PlainTextBody, IReadOnlyList<RenderedLink> Links);

public interface ICampaignEmailRenderer
{
    /// <summary>Renders ONCE per campaign, not per recipient — the returned HTML/plain-text bodies
    /// contain a literal <c>{{UNSUBSCRIBE_URL}}</c> placeholder (and the HTML one a literal
    /// <c>{{OPEN_PIXEL_URL}}</c> placeholder) that the caller substitutes per recipient at queue time (see
    /// the send pipeline), so this (branded, potentially large) template is never rebuilt per
    /// recipient. Every link (the CTA and each http(s) URL in the body, which is now rendered as a link) is a
    /// <see cref="RenderedLink.Placeholder"/> in both bodies, replaced per recipient as well.
    /// <paramref name="supportEmail"/> is the public address named in the footer ("Reply to …").</summary>
    CampaignEmailContent Render(
        string subject,
        string bodyText,
        string? ctaLabel,
        string? ctaUrl,
        string brandName,
        string? logoUrl,
        string primaryColor,
        string? supportEmail = null);
}
