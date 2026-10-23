namespace CreatorPlatform.Shared.Application.Exceptions;

/// <summary>A 410: the thing existed but is no longer available (e.g. a buyer's download link turned off by a refund).</summary>
public sealed class GoneException : Exception
{
    public GoneException(string message, string code)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
