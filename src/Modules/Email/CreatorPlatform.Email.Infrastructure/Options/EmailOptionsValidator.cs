using Microsoft.Extensions.Options;
using MimeKit;

namespace CreatorPlatform.Email.Infrastructure.Options;

/// <summary>Checked when the host starts (<c>ValidateOnStart</c>), after <see cref="EmailOptionsDefaults"/> applied.</summary>
public sealed class EmailOptionsValidator(bool isDevelopment) : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        var failures = new List<string>();

        if (!MailboxAddress.TryParse(options.FromAddress, out _))
            failures.Add("Email:FromAddress must be a valid email address.");

        if (options.ThrottleBackoffMinutes <= 0)
            failures.Add("Email:ThrottleBackoffMinutes must be positive.");

        switch (options.Provider)
        {
            case EmailProvider.Acs:
                if (string.IsNullOrWhiteSpace(options.ConnectionString))
                    failures.Add("Email:ConnectionString (Azure Communication Services) is required when Email:Provider is Acs.");
                break;

            case EmailProvider.Smtp:
                if (!isDevelopment && !options.AllowSmtpOutsideDevelopment)
                    failures.Add("Email:Provider Smtp is for Development (the local mail catcher). Outside Development it needs " +
                                 "Email:AllowSmtpOutsideDevelopment=true, so a real host never drops its mail into a catcher by mistake.");
                if (string.IsNullOrWhiteSpace(options.Smtp.Host))
                    failures.Add("Email:Smtp:Host is required when Email:Provider is Smtp.");
                if (options.Smtp.Port is < 1 or > 65535)
                    failures.Add("Email:Smtp:Port must be between 1 and 65535.");
                if (string.IsNullOrEmpty(options.Smtp.UserName) != string.IsNullOrEmpty(options.Smtp.Password))
                    failures.Add("Email:Smtp:UserName and Email:Smtp:Password are set together, or neither.");
                break;

            default:
                failures.Add("Email:Provider must be Acs or Smtp.");
                break;
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
