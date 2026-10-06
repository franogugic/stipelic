namespace CreatorPlatform.Shared.Application.Exceptions;

public sealed class ConflictException : Exception
{
    public const string DefaultCode = "CONFLICT";

    public ConflictException(string message, string code = DefaultCode, object? details = null)
        : base(message)
    {
        Code = code;
        Details = details;
    }

    /// <summary>Machine-readable code returned in the 409 body, so clients branch on it instead of the message.</summary>
    public string Code { get; }

    /// <summary>Optional data the client needs to explain the conflict (e.g. <c>{ used, limit }</c>), returned as
    /// <c>details</c> in the 409 body.</summary>
    public object? Details { get; }
}
