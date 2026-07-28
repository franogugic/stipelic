namespace CreatorPlatform.Email.Application.Templates;

public static class PayoutRequestedTemplate
{
    public static string BuildSubject(string creatorName, string formattedAmount)
    {
        return $"Payout request: {creatorName} — {formattedAmount}";
    }

    public static string BuildHtml(string creatorName, string creatorSlug, string formattedAmount, string adminPayoutsUrl)
    {
        return $"""
            <h1>New payout request</h1>
            <p><strong>{creatorName}</strong> (/{creatorSlug}) requested a payout of <strong>{formattedAmount}</strong>.</p>
            <p><a href="{adminPayoutsUrl}">Review in the payouts admin queue</a></p>
            """;
    }

    public static string BuildPlainText(string creatorName, string creatorSlug, string formattedAmount, string adminPayoutsUrl)
    {
        return $"""
            New payout request

            {creatorName} (/{creatorSlug}) requested a payout of {formattedAmount}.

            Review it here:
            {adminPayoutsUrl}
            """;
    }
}
