using System.Net.Mail;
using System.Text.RegularExpressions;
using CreatorPlatform.Creators.Application.Dtos;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Creators.Application.Services;

internal sealed record CreatorSettingsUpdateValues(
    string? SupportEmail,
    string BrandName,
    string? LogoUrl,
    string PrimaryColor,
    string Timezone,
    string Language);

internal static partial class CreatorSettingsUpdateValidator
{
    private const string DefaultPrimaryColor = "#111827";
    private const string DefaultTimezone = "Europe/Sarajevo";
    private const string DefaultLanguage = "en";
    private const string OnlySupportedLanguage = "en";
    private const int SupportEmailMaxLength = 100;
    private const int BrandNameMaxLength = 50;
    private const int LogoUrlMaxLength = 500;
    private const int TimezoneMaxLength = 50;
    private const int LanguageMaxLength = 10;

    public static CreatorSettingsUpdateValues Validate(
        UpdateCreatorSettingsRequestDto request,
        string fallbackBrandName)
    {
        return new CreatorSettingsUpdateValues(
            NormalizeOptionalEmail(request.SupportEmail),
            NormalizeOptionalText(request.BrandName, BrandNameMaxLength) ?? fallbackBrandName,
            NormalizeOptionalUrl(request.LogoUrl),
            NormalizePrimaryColor(request.PrimaryColor),
            NormalizeTimezone(request.Timezone),
            NormalizeLanguage(request.Language));
    }

    private static string? NormalizeOptionalEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalized = email.Trim().ToLowerInvariant();

        if (normalized.Length > SupportEmailMaxLength)
            throw new BadRequestException($"Support email cannot be longer than {SupportEmailMaxLength} characters.");

        try
        {
            _ = new MailAddress(normalized);
        }
        catch (FormatException)
        {
            throw new BadRequestException("Support email is not valid.");
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
            throw new BadRequestException($"Value cannot be longer than {maxLength} characters.");

        return normalized;
    }

    private static string? NormalizeOptionalUrl(string? value)
    {
        var normalized = NormalizeOptionalText(value, LogoUrlMaxLength);
        if (normalized is null)
            return null;

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new BadRequestException("Logo URL must be a valid HTTP or HTTPS URL.");
        }

        return normalized;
    }

    private static string NormalizePrimaryColor(string? primaryColor)
    {
        if (string.IsNullOrWhiteSpace(primaryColor))
            return DefaultPrimaryColor;

        var normalized = primaryColor.Trim();

        if (!HexColorRegex().IsMatch(normalized))
            throw new BadRequestException("Primary color must be a valid hex color.");

        return normalized;
    }

    private static string NormalizeTimezone(string? timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
            return DefaultTimezone;

        var normalized = timezone.Trim();

        if (normalized.Length > TimezoneMaxLength)
            throw new BadRequestException($"Timezone cannot be longer than {TimezoneMaxLength} characters.");

        if (!TimezoneRegex().IsMatch(normalized))
            throw new BadRequestException("Timezone is not valid.");

        return normalized;
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return DefaultLanguage;

        var normalized = language.Trim().ToLowerInvariant();

        if (normalized.Length > LanguageMaxLength)
            throw new BadRequestException($"Language cannot be longer than {LanguageMaxLength} characters.");

        if (!LanguageRegex().IsMatch(normalized))
            throw new BadRequestException("Language is not valid.");

        if (normalized != OnlySupportedLanguage)
            throw new BadRequestException("English is the only supported language right now.");

        return normalized;
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColorRegex();

    [GeneratedRegex("^[a-z]{2}(-[a-z]{2})?$")]
    private static partial Regex LanguageRegex();

    [GeneratedRegex("^[A-Za-z0-9_./+-]+$")]
    private static partial Regex TimezoneRegex();
}
