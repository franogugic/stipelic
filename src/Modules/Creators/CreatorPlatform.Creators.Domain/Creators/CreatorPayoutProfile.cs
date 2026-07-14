namespace CreatorPlatform.Creators.Domain.Creators;

/// <summary>Bank details for BankTransfer-mode creators. 1:1 with <see cref="Creator"/>, same shared-PK pattern as <see cref="CreatorSettings"/>.</summary>
public sealed class CreatorPayoutProfile
{
    private CreatorPayoutProfile()
    {
    }

    private CreatorPayoutProfile(
        Creator creator,
        string accountHolderName,
        string iban,
        string bankCountryCode,
        DateTimeOffset createdAt)
    {
        Creator = creator;
        AccountHolderName = accountHolderName;
        Iban = iban;
        BankCountryCode = bankCountryCode;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static CreatorPayoutProfile Create(
        Creator creator,
        string accountHolderName,
        string iban,
        string bankCountryCode,
        DateTimeOffset createdAt)
    {
        return new CreatorPayoutProfile(creator, accountHolderName, iban, bankCountryCode, createdAt);
    }

    public void Update(
        string accountHolderName,
        string iban,
        string bankCountryCode,
        DateTimeOffset updatedAt)
    {
        AccountHolderName = accountHolderName;
        Iban = iban;
        BankCountryCode = bankCountryCode;
        UpdatedAt = updatedAt;
    }

    public int CreatorId { get; private set; }

    public Creator Creator { get; private set; } = null!;

    public string AccountHolderName { get; private set; } = string.Empty;

    /// <summary>Normalized (no spaces, uppercase) — see <see cref="IbanValidator.Normalize"/>.</summary>
    public string Iban { get; private set; } = string.Empty;

    public string BankCountryCode { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
