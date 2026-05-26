namespace CreatorPlatform.Auth.Application.Exceptions;

public sealed class TooManyLoginAttemptsException : Exception
{
    public TooManyLoginAttemptsException()
        : base("Too many login attempts. Please try again later.")
    {
    }
}
