using System.Globalization;

namespace CreatorPlatform.Shared.Application.Csv;

/// <summary>RFC 4180 CSV output with a guard against CSV/formula injection — every export builds its rows here so
/// the escaping rules can't drift between exports.</summary>
public static class CsvFormatter
{
    /// <summary>RFC 4180 record separator.</summary>
    public const string LineEnding = "\r\n";

    /// <summary>Characters that make a spreadsheet treat a cell as a formula (or, for tab/CR, let one through).</summary>
    private static readonly char[] FormulaTriggers = ['=', '+', '-', '@', '\t', '\r'];

    private static readonly char[] CharactersRequiringQuotes = [',', '"', '\r', '\n'];

    /// <summary>One CSV line (without the line ending) from already-plain field values.</summary>
    public static string Row(params string?[] fields) => string.Join(',', fields.Select(Field));

    /// <summary>One field: a value starting with = + - @ tab or CR is prefixed with ' so spreadsheets show it as
    /// text instead of evaluating it; then the value is quoted (inner quotes doubled) when it contains a comma, a
    /// quote, CR or LF. Null becomes an empty field.</summary>
    public static string Field(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (Array.IndexOf(FormulaTriggers, value[0]) >= 0)
            value = "'" + value;

        return value.IndexOfAny(CharactersRequiringQuotes) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    /// <summary>ISO 8601 UTC with second precision, e.g. 2026-10-02T09:15:00Z.</summary>
    public static string Timestamp(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>Cents as a decimal string with exactly two decimals and a dot, e.g. 2900 → "29.00", 5 → "0.05",
    /// -150 → "-1.50". Integer arithmetic (no floating point) and culture-independent.</summary>
    public static string Money(long cents)
    {
        var sign = cents < 0 ? "-" : string.Empty;
        var absolute = cents < 0 ? -(decimal)cents : cents;
        var units = decimal.Truncate(absolute / 100);
        var remainder = absolute - units * 100;
        return string.Create(CultureInfo.InvariantCulture, $"{sign}{units:0}.{remainder:00}");
    }
}
