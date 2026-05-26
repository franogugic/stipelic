namespace CreatorPlatform.Auth.Application.Exceptions;

public sealed class EmailNotVerifiedException : Exception
{
    public EmailNotVerifiedException()
        : base("Email must be verified before login.")
    {
    }
}
