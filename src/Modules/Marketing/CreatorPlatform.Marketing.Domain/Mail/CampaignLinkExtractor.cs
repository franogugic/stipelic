using System.Text.RegularExpressions;
using CreatorPlatform.Marketing.Domain.Campaigns;

namespace CreatorPlatform.Marketing.Domain.Mail;

/// <summary>A run of a body line: plain text, or a URL to be linked.</summary>
public sealed record BodySegment(string Text, bool IsUrl);

/// <summary>A distinct link of an email, in order of first appearance.</summary>
public sealed record ExtractedLink(int Position, string Url, CampaignLinkKind Kind, string? Label);

/// <summary>
/// Finds the links of a campaign email: every <c>http(s)://</c> URL in the body text and the CTA button URL. The
/// match is strict: it starts at <c>http://</c> or <c>https://</c>, stops at whitespace, quotes and angle
/// brackets, and leaves out trailing sentence punctuation (<c>. , ; : ! ?</c>) and a closing bracket that has no
/// opening partner inside the URL ("(see https://x.test/a)"). A match that is not an absolute http(s) URI, or is
/// longer than <see cref="MailContentRules.MaxCtaUrlLength"/>, stays plain text.
/// </summary>
public static partial class CampaignLinkExtractor
{
    [GeneratedRegex(@"https?://[^\s<>""']+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlCandidate();

    private const string TrailingPunctuation = ".,;:!?";

    /// <summary>Splits one line into text and URL runs, in order; concatenated they give the line back.</summary>
    public static List<BodySegment> Segment(string line)
    {
        var segments = new List<BodySegment>();
        var cursor = 0;

        foreach (Match match in UrlCandidate().Matches(line))
        {
            var url = TrimUrl(match.Value);
            if (!IsLinkable(url))
                continue;

            if (match.Index > cursor)
                segments.Add(new BodySegment(line[cursor..match.Index], false));

            segments.Add(new BodySegment(url, true));
            cursor = match.Index + url.Length;
        }

        if (cursor < line.Length)
            segments.Add(new BodySegment(line[cursor..], false));

        return segments;
    }

    /// <summary>
    /// The distinct links of the email: body URLs first (they appear above the button), then the CTA. One URL is one
    /// link however often it appears; when the button uses a URL that is also in the text, the link is the button
    /// (with its label), at the position where the URL first appears.
    /// </summary>
    public static List<ExtractedLink> Extract(IEnumerable<string> bodyLines, string? ctaLabel, string? ctaUrl)
    {
        var ordered = bodyLines
            .SelectMany(Segment)
            .Where(segment => segment.IsUrl)
            .Select(segment => segment.Text)
            .ToList();

        var hasCta = !string.IsNullOrWhiteSpace(ctaLabel) && !string.IsNullOrWhiteSpace(ctaUrl);
        if (hasCta)
            ordered.Add(ctaUrl!.Trim());

        return ordered
            .Distinct(StringComparer.Ordinal)
            .Select((url, index) => hasCta && url == ctaUrl!.Trim()
                ? new ExtractedLink(index + 1, url, CampaignLinkKind.Button, ctaLabel!.Trim())
                : new ExtractedLink(index + 1, url, CampaignLinkKind.Text, null))
            .ToList();
    }

    private static string TrimUrl(string candidate)
    {
        var url = candidate;
        while (url.Length > 0)
        {
            var last = url[^1];
            if (TrailingPunctuation.Contains(last) || IsUnbalancedCloser(url, last))
                url = url[..^1];
            else
                break;
        }

        return url;
    }

    private static bool IsUnbalancedCloser(string url, char last)
    {
        var opener = last switch
        {
            ')' => '(',
            ']' => '[',
            '}' => '{',
            _ => '\0'
        };

        return opener != '\0' && url.Count(c => c == last) > url.Count(c => c == opener);
    }

    private static bool IsLinkable(string url) =>
        url.Length <= MailContentRules.MaxCtaUrlLength
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrEmpty(uri.Host);
}
