namespace CreatorPlatform.Marketing.Application.Interfaces;

/// <summary>The 400 codes of a contacts import that can't be processed at all (as opposed to invalid rows).</summary>
public static class ContactImportErrors
{
    public const string FileMissing = "FILE_MISSING";
    public const string FileTooLarge = "FILE_TOO_LARGE";
    public const string FileEmpty = "FILE_EMPTY";
    public const string TooManyRows = "TOO_MANY_ROWS";
    public const string NoEmailColumn = "NO_EMAIL_COLUMN";
    public const string InvalidEncoding = "INVALID_ENCODING";
    public const string InvalidCsv = "INVALID_CSV";
    public const string ConsentRequired = "CONSENT_REQUIRED";
}
