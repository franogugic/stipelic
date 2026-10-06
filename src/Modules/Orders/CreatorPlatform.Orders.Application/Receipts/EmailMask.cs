namespace CreatorPlatform.Orders.Application.Receipts;

/// <summary>Masks an email for a page anyone with the link can open: the first character and the domain stay,
/// e.g. "ana.kovacevic@gmail.com" → "a•••@gmail.com".</summary>
public static class EmailMask
{
    private const string Hidden = "•••";

    public static string Mask(string email)
    {
        var trimmed = email.Trim();
        var at = trimmed.LastIndexOf('@');
        if (at <= 0 || at == trimmed.Length - 1)
            return Hidden;

        return $"{trimmed[0]}{Hidden}{trimmed[at..]}";
    }
}
