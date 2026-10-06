using System.Globalization;

namespace CreatorPlatform.Email.Application.Templates;

/// <summary>"€29.00", "$12.50"; other currencies as "1,200.00 RSD". The API's currency names ("Eur") are accepted.</summary>
public static class EmailMoney
{
    public static string Format(long amountCents, string currency)
    {
        var code = currency.Trim().ToUpperInvariant();
        var amount = (amountCents / 100m).ToString("#,0.00", CultureInfo.InvariantCulture);
        return code switch
        {
            "EUR" => $"€{amount}",
            "USD" => $"${amount}",
            "GBP" => $"£{amount}",
            _ => $"{amount} {code}",
        };
    }
}
