namespace CreatorPlatform.Creators.Application.Dtos;

public sealed record PayoutProfileResponseDto
{
    public string AccountHolderName { get; init; } = string.Empty;

    /// <summary>Never the full IBAN — first 4 + last 4 characters visible, the rest masked.</summary>
    public string MaskedIban { get; init; } = string.Empty;

    public string BankCountryCode { get; init; } = string.Empty;
}

public sealed record UpdatePayoutProfileRequestDto
{
    public string AccountHolderName { get; init; } = string.Empty;
    public string Iban { get; init; } = string.Empty;
    public string BankCountryCode { get; init; } = string.Empty;
}
