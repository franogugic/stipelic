namespace CreatorPlatform.Shared.Application.Exceptions;

public sealed class BadRequestException : Exception
{
    public const string DefaultCode = "BAD_REQUEST";

    public BadRequestException(string message, string code = DefaultCode)
        : base(message)
    {
        Code = code;
    }

    /// <summary>Machine-readable code returned in the 400 body, so clients branch on it instead of the message.</summary>
    public string Code { get; }
}
