namespace CreatorPlatform.Shared.Application.Exceptions;

public sealed class NotFoundException : Exception
{
    public const string DefaultCode = "NOT_FOUND";

    /// <param name="code">Machine-readable code in the error response, for a client that has to tell this 404 apart
    /// (e.g. "unsubscribe_link_invalid").</param>
    public NotFoundException(string message, string code = DefaultCode)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
