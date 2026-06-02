namespace CreatorPlatform.Shared.Application.Exceptions;

public sealed class InternalServerException : Exception
{
    public InternalServerException(string message)
        : base(message)
    {
    }
}
