using CreatorPlatform.Creators.Domain.Creators;

namespace CreatorPlatform.MoneyPath.Tests.Creators;

public class IbanValidatorTests
{
    // Canonical example IBANs (one per country the task asks for), each with a genuinely valid mod-97 checksum.
    [Theory]
    [InlineData("HR1210010051863000160")]
    [InlineData("RS35260005601001611379")]
    [InlineData("BA391290079401028494")]
    [InlineData("DE89370400440532013000")]
    public void IsValid_ValidExamples_ReturnsTrue(string iban)
    {
        Assert.True(IbanValidator.IsValid(IbanValidator.Normalize(iban)));
    }

    [Fact]
    public void IsValid_SwappedChecksumDigits_ReturnsFalse()
    {
        // DE89370400440532013000 with the check digits swapped (89 -> 98) breaks the mod-97 checksum
        // while keeping the structure (2 letters + 2 digits + alnum) valid.
        var iban = IbanValidator.Normalize("DE98370400440532013000");
        Assert.False(IbanValidator.IsValid(iban));
    }

    [Fact]
    public void Normalize_SpacesAndLowercase_NormalizedAndStillValid()
    {
        var normalized = IbanValidator.Normalize(" de89 3704 0044 0532 0130 00 ");

        Assert.Equal("DE89370400440532013000", normalized);
        Assert.True(IbanValidator.IsValid(normalized));
    }

    [Theory]
    [InlineData("DE8937040044053201300012345678901234")] // too long (> 34)
    [InlineData("DE89")] // too short (< 15)
    [InlineData("12DE370400440532013000")] // doesn't start with 2 letters
    [InlineData("DEAB370400440532013000")] // check digits aren't numeric
    public void IsValid_InvalidLengthOrStructure_ReturnsFalse(string iban)
    {
        Assert.False(IbanValidator.IsValid(IbanValidator.Normalize(iban)));
    }
}
