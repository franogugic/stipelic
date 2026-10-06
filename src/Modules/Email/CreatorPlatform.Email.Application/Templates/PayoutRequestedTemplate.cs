using System.Globalization;
using static CreatorPlatform.Email.Application.Templates.EmailLayout;

namespace CreatorPlatform.Email.Application.Templates;

/// <summary>What the admin's payout-request notice shows.</summary>
public sealed record PayoutRequestedEmail(
    string AccountHolderName,
    string CreatorName,
    string CreatorSlug,
    int AmountCents,
    string Currency,
    string Iban,
    string BankCountryCode,
    DateTimeOffset RequestedAt,
    int BalanceAfterCents);

/// <summary><c>designer-prototype/emails/admin-payout-request.html</c>.</summary>
public static class PayoutRequestedTemplate
{
    public static RenderedEmail Render(EmailBranding branding, PayoutRequestedEmail payout, string adminPayoutsUrl)
    {
        var amount = EmailMoney.Format(payout.AmountCents, payout.Currency);
        var who = string.IsNullOrWhiteSpace(payout.AccountHolderName)
            ? payout.CreatorName
            : $"{payout.AccountHolderName} · {payout.CreatorName}";
        var subject = $"Payout request: {payout.CreatorName} — {amount}";

        var card =
            $"""<p class="muted" style="margin:0 0 4px; font-family:Geist, -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; font-size:13px; line-height:20px; color:#6B695F;">New payout request</p>""" +
            $"""<p class="text" style="margin:0 0 8px; font-family:'Instrument Serif', Georgia, 'Times New Roman', serif; font-size:56px; line-height:58px; letter-spacing:-1px; color:#17160F;">{E(amount)}</p>""" +
            $"""<p class="text" style="margin:0 0 20px; font-family:Geist, -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; font-size:16px; line-height:25px; color:#17160F;">{E(who)}</p>""" +
            KeyValues(
                Row("Workspace", payout.CreatorSlug),
                ("IBAN", MonoValue(FormatIban(payout.Iban)), true),
                Row("Bank country", CountryName(payout.BankCountryCode)),
                Row("Requested", DateTime(payout.RequestedAt)),
                Row("Balance after payout", EmailMoney.Format(payout.BalanceAfterCents, payout.Currency))) +
            Button("Open payout queue", adminPayoutsUrl) +
            Note("Mark it as paid with the bank reference once the transfer is sent — the creator sees the reference in their payout history.", "0");

        var html = Document(
            subject,
            $"{who} requested a {amount} bank payout.",
            LumaHeader(branding.LogoUrl, badge: "Admin"),
            card,
            "You’re receiving this because you’re a Luma platform admin.<br>Luma · Payout operations");

        var text = $"""
            New payout request: {amount}
            {who}

            Workspace: {payout.CreatorSlug}
            IBAN: {FormatIban(payout.Iban)}
            Bank country: {CountryName(payout.BankCountryCode)}
            Requested: {DateTime(payout.RequestedAt)}
            Balance after payout: {EmailMoney.Format(payout.BalanceAfterCents, payout.Currency)}

            Open the payout queue:
            {adminPayoutsUrl}

            Mark it as paid with the bank reference once the transfer is sent — the creator sees the reference in their payout history.
            """;

        return new RenderedEmail(subject, html, text);
    }

    /// <summary>"RS352600056010016113 79" → groups of four, as banks print it.</summary>
    public static string FormatIban(string iban)
    {
        var compact = new string(iban.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        return string.Join(' ', compact.Chunk(4).Select(chunk => new string(chunk)));
    }

    /// <summary>"RS" → "Serbia"; an unknown code is shown as is.</summary>
    public static string CountryName(string code)
    {
        try
        {
            return new RegionInfo(code).EnglishName;
        }
        catch (ArgumentException)
        {
            return code;
        }
    }
}
