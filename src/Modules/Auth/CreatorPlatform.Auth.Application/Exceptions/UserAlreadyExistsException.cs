namespace CreatorPlatform.Auth.Application.Exceptions;

public sealed class UserAlreadyExistsException : Exception
{
    public UserAlreadyExistsException()
        : base("An account with this email already exists.")
    {
    }
}
