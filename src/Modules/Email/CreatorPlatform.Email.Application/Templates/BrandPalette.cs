using System.Globalization;

namespace CreatorPlatform.Email.Application.Templates;

/// <summary>
/// A creator's brand colour turned into email-safe colours, ported from the web's <c>brandStyle()</c>: the colour fills
/// buttons and tiles as is; text in that colour is shaded until it reaches 4.5:1 against the card (separately for the
/// light and the dark theme); button text is white or ink, whichever reads better.
/// </summary>
public sealed record BrandPalette(string Fill, string OnFill, string TextLight, string TextDark)
{
    /// <summary>Luma's own accent, used when a creator has no (valid) brand colour.</summary>
    public const string LumaAccent = "#D4FA5A";
    public const string DefaultBrandTextDark = "#F49A86";

    private const string Ink = "#1C1612";
    private const string LightCard = "#FFFFFF";
    private const string DarkCard = "#181816";

    public static BrandPalette Luma { get; } = From(LumaAccent);

    /// <summary>The palette for a creator's colour; Luma's when it is missing or not a #RRGGBB value.</summary>
    public static BrandPalette From(string? hex)
    {
        var color = IsHex(hex) ? hex!.ToUpperInvariant() : LumaAccent;
        var rgb = Parse(color);
        var onWhite = Contrast(rgb, (255, 255, 255));
        var onInk = Contrast(rgb, Parse(Ink));
        var onFill = onWhite >= 4.5 || onWhite >= onInk ? "#FFFFFF" : Ink;
        return new BrandPalette(color, onFill, ShadeFor(rgb, LightCard, 4.5), ShadeFor(rgb, DarkCard, 4.5));
    }

    public static bool IsHex(string? value) =>
        value is { Length: 7 } && value[0] == '#' && value[1..].All(Uri.IsHexDigit);

    private static (int R, int G, int B) Parse(string hex) => (
        int.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber),
        int.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber),
        int.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber));

    private static string ToHex((int R, int G, int B) rgb) => $"#{rgb.R:X2}{rgb.G:X2}{rgb.B:X2}";

    private static double Luminance((int R, int G, int B) rgb)
    {
        static double Channel(int value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(rgb.R) + 0.7152 * Channel(rgb.G) + 0.0722 * Channel(rgb.B);
    }

    private static double Contrast((int R, int G, int B) a, (int R, int G, int B) b)
    {
        var (hi, lo) = (Math.Max(Luminance(a), Luminance(b)), Math.Min(Luminance(a), Luminance(b)));
        return (hi + 0.05) / (lo + 0.05);
    }

    /// <summary>Mixes the colour toward black (light background) or white (dark) until it passes the ratio.</summary>
    private static string ShadeFor((int R, int G, int B) color, string background, double ratio)
    {
        var bg = Parse(background);
        var target = Luminance(bg) > 0.4 ? (0, 0, 0) : (255, 255, 255);
        for (var step = 0; step <= 25; step++)
        {
            var t = step * 0.04;
            var mixed = (
                (int)Math.Round(color.R + (target.Item1 - color.R) * t),
                (int)Math.Round(color.G + (target.Item2 - color.G) * t),
                (int)Math.Round(color.B + (target.Item3 - color.B) * t));
            if (Contrast(mixed, bg) >= ratio)
                return ToHex(mixed);
        }
        return ToHex(target);
    }
}
