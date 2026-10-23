using System.ComponentModel.DataAnnotations;

namespace CreatorPlatform.Marketing.Application.Services;

/// <summary>An imported email checked by the same rules as a page sign-up (<c>EmailCaptureRequestDto</c>: required,
/// <see cref="EmailAddressAttribute"/>, at most 254 characters), after trimming, and normalised the same way
/// (lower-cased). <see cref="Reason"/> is set when it fails: <c>EMPTY</c>, <c>TOO_LONG</c> or <c>INVALID_EMAIL</c>.</summary>
public readonly record struct ContactEmailCheck(string Value, string? Normalized, string? Reason);

public static class ContactEmailRules
{
    public const int MaxLength = 254;

    public const string Empty = "EMPTY";
    public const string TooLong = "TOO_LONG";
    public const string InvalidEmail = "INVALID_EMAIL";

    private static readonly EmailAddressAttribute EmailAddress = new();

    public static ContactEmailCheck Check(string? raw)
    {
        var value = raw?.Trim() ?? string.Empty;

        if (value.Length == 0)
            return new ContactEmailCheck(value, null, Empty);
        if (value.Length > MaxLength)
            return new ContactEmailCheck(value[..MaxLength], null, TooLong);
        if (!EmailAddress.IsValid(value))
            return new ContactEmailCheck(value, null, InvalidEmail);

        return new ContactEmailCheck(value, value.ToLowerInvariant(), null);
    }
}
