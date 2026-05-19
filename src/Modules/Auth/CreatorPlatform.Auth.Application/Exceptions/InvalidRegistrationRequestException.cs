namespace CreatorPlatform.Auth.Application.Exceptions;

public sealed class InvalidRegistrationRequestException : Exception
{
    public InvalidRegistrationRequestException(string message)
        : base(message)
    {
    }
}
