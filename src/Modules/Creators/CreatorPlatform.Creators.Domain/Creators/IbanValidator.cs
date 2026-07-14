using System.Text.RegularExpressions;

namespace CreatorPlatform.Creators.Domain.Creators;

public static partial class IbanValidator
{
    private const int MinLength = 15;
    private const int MaxLength = 34;

    /// <summary>Trims, strips whitespace and uppercases — the canonical on-the-wire IBAN form.</summary>
    public static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var buffer = new char[value.Length];
        var count = 0;

        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
                continue;

            buffer[count++] = char.ToUpperInvariant(c);
        }

        return new string(buffer, 0, count);
    }

    /// <summary>Expects an already-normalized value (see <see cref="Normalize"/>).</summary>
    public static bool IsValid(string value)
    {
        if (value.Length < MinLength || value.Length > MaxLength)
            return false;

        if (!StructureRegex().IsMatch(value))
            return false;

        return ComputeMod97(value) == 1;
    }

    // ISO 7064 mod-97-10 checksum: move the first 4 characters to the end, expand letters to their
    // two-digit values (A=10..Z=35), then take the remainder of the resulting number modulo 97 —
    // computed digit-by-digit rather than materializing the (potentially huge) number, so this stays
    // O(n) with O(1) extra space and no BigInteger allocations.
    private static int ComputeMod97(string iban)
    {
        var remainder = 0;

        for (var i = 0; i < iban.Length; i++)
        {
            var c = iban[(i + 4) % iban.Length];
            remainder = AccumulateDigits(remainder, c);
        }

        return remainder;
    }

    private static int AccumulateDigits(int remainder, char c)
    {
        if (c is >= '0' and <= '9')
            return (remainder * 10 + (c - '0')) % 97;

        var value = c - 'A' + 10; // A=10 ... Z=35
        remainder = (remainder * 10 + value / 10) % 97;
        remainder = (remainder * 10 + value % 10) % 97;
        return remainder;
    }

    [GeneratedRegex(@"^[A-Z]{2}[0-9]{2}[A-Z0-9]+$")]
    private static partial Regex StructureRegex();
}
