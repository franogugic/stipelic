namespace CreatorPlatform.Shared.Infrastructure.Persistence;

/// <summary>Builds SQL LIKE patterns from user input, so a search term's own wildcards match literally.</summary>
public static class LikePatterns
{
    /// <summary>The escape character the patterns use — pass it to <c>EF.Functions.Like</c> or write
    /// <c>ESCAPE '\'</c> in raw SQL.</summary>
    public const string EscapeCharacter = "\\";

    /// <summary>Escapes LIKE wildcards so a user's term matches literally: \ → \\, % → \%, _ → \_.</summary>
    public static string Escape(string term) => term
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_");

    /// <summary>"%term%" with the term escaped — a substring match.</summary>
    public static string Contains(string term) => "%" + Escape(term) + "%";
}
