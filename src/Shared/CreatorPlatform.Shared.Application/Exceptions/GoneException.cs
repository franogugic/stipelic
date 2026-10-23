namespace CreatorPlatform.Shared.Application.Exceptions;

/// <summary>A 410: the thing existed but is no longer available (e.g. a buyer's download link turned off by a refund).</summary>
public sealed class GoneException : Exception
{
    /// <param name="body">What the client may still show about the gone thing; its properties are written at the top
    /// level of the error body, next to <c>statusCode</c>, <c>message</c> and <c>code</c>.</param>
    public GoneException(string message, string code, object? body = null)
        : base(message)
    {
        Code = code;
        Body = body;
    }

    public string Code { get; }

    public object? Body { get; }
}
