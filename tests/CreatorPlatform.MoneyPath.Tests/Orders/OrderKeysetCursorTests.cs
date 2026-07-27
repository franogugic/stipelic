using CreatorPlatform.Orders.Domain.Orders;

namespace CreatorPlatform.MoneyPath.Tests.Orders;

public class OrderKeysetCursorTests
{
    private static readonly DateTimeOffset Cursor = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid CursorId = Guid.Parse("00000000-0000-0000-0000-000000000010");

    [Fact]
    public void IsBeforeCursor_EarlierCreatedAt_ReturnsTrue()
    {
        var earlier = Cursor.AddSeconds(-1);
        Assert.True(OrderKeysetCursor.IsBeforeCursor(earlier, Guid.NewGuid(), Cursor, CursorId));
    }

    [Fact]
    public void IsBeforeCursor_LaterCreatedAt_ReturnsFalse()
    {
        var later = Cursor.AddSeconds(1);
        Assert.False(OrderKeysetCursor.IsBeforeCursor(later, Guid.NewGuid(), Cursor, CursorId));
    }

    [Fact]
    public void IsBeforeCursor_SameCreatedAt_SmallerPublicId_ReturnsTrue()
    {
        var smallerId = Guid.Parse("00000000-0000-0000-0000-000000000005");
        Assert.True(OrderKeysetCursor.IsBeforeCursor(Cursor, smallerId, Cursor, CursorId));
    }

    [Fact]
    public void IsBeforeCursor_SameCreatedAt_LargerPublicId_ReturnsFalse()
    {
        var largerId = Guid.Parse("00000000-0000-0000-0000-000000000020");
        Assert.False(OrderKeysetCursor.IsBeforeCursor(Cursor, largerId, Cursor, CursorId));
    }

    [Fact]
    public void IsBeforeCursor_ExactCursorRow_ReturnsFalse()
    {
        // The cursor row itself must never be returned again — no duplicate on the next page.
        Assert.False(OrderKeysetCursor.IsBeforeCursor(Cursor, CursorId, Cursor, CursorId));
    }
}
