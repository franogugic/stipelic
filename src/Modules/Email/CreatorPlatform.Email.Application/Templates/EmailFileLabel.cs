using System.Globalization;

namespace CreatorPlatform.Email.Application.Templates;

/// <summary>"PDF · 8.2 MB": what a product's document is, under its name in delivery emails.</summary>
public static class EmailFileLabel
{
    public static string? From(string? contentType, long? sizeBytes)
    {
        if (contentType is null || sizeBytes is null)
            return null;

        var kind = contentType switch
        {
            "application/pdf" => "PDF",
            "application/epub+zip" => "EPUB",
            "application/zip" => "ZIP",
            _ => "File",
        };
        return $"{kind} · {Size(sizeBytes.Value)}";
    }

    /// <summary>Binary units, one decimal under 100 ("8.2 MB", "100 MB", "640 KB").</summary>
    public static string Size(long bytes)
    {
        const double kb = 1024, mb = kb * 1024;
        if (bytes >= mb)
            return Format(bytes / mb) + " MB";
        if (bytes >= kb)
            return Format(bytes / kb) + " KB";
        return bytes.ToString(CultureInfo.InvariantCulture) + " B";
    }

    private static string Format(double value) =>
        value >= 100
            ? Math.Round(value).ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.#", CultureInfo.InvariantCulture);
}
