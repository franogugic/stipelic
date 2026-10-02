namespace CreatorPlatform.Shared.Application.Exceptions;

public sealed class ConflictException : Exception
{
    public const string DefaultCode = "CONFLICT";

    public ConflictException(string message, string code = DefaultCode)
        : base(message)
    {
        Code = code;
    }

    /// <summary>Machine-readable code returned in the 409 body, so clients branch on it instead of the message.</summary>
    public string Code { get; }
}
