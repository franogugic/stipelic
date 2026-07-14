namespace CreatorPlatform.Shared.Domain.Money;

public static class PlatformFee
{
    /// <summary>
    /// Computes the platform's fee in cents for a given amount and fee rate.
    /// Rounds down (floor) in the creator's favor — e.g. 999 cents at 500 bps (5%) yields 49, not 50.
    /// </summary>
    /// <param name="amountCents">Gross amount in cents. Must be non-negative.</param>
    /// <param name="feeBasisPoints">Fee rate in basis points (1/100 of a percent). Must be in [0, 10000].</param>
    public static int Calculate(int amountCents, int feeBasisPoints)
    {
        if (amountCents < 0)
            throw new ArgumentOutOfRangeException(nameof(amountCents), amountCents, "Amount cannot be negative.");

        if (feeBasisPoints < 0 || feeBasisPoints > 10_000)
            throw new ArgumentOutOfRangeException(nameof(feeBasisPoints), feeBasisPoints, "Fee basis points must be between 0 and 10000.");

        // long intermediate avoids overflow: amountCents (max ~2.1B) * feeBasisPoints (max 10_000) ~= 2.1e13, well within long range.
        return (int)((long)amountCents * feeBasisPoints / 10_000);
    }
}
