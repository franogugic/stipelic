using CreatorPlatform.Shared.Application.Time;

namespace CreatorPlatform.MoneyPath.Tests.Shared;

public class CreatorTimeZoneTests
{
    [Theory]
    [InlineData("Europe/Zagreb", "Europe/Zagreb")]
    [InlineData(" Europe/Sarajevo ", "Europe/Sarajevo")]
    [InlineData(null, "UTC")]
    [InlineData("", "UTC")]
    [InlineData("Mars/Olympus", "UTC")] // passes the settings' shape check, but no such zone
    public void Resolve_KnownIanaZonesPassThrough_AnythingElseIsUtc(string? stored, string expected)
    {
        Assert.Equal(expected, CreatorTimeZone.Resolve(stored));
    }

    [Fact]
    public void LocalMonthStart_IsMidnightOnTheFirst_InTheZone()
    {
        // 30 Sep 2026 23:30 UTC is already 1 Oct 01:30 in Zagreb (UTC+2 in summer time).
        var start = CreatorTimeZone.LocalMonthStart(new DateTimeOffset(2026, 9, 30, 23, 30, 0, TimeSpan.Zero), "Europe/Zagreb");

        Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(2)), start);
    }
}
