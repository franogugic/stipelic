namespace CreatorPlatform.Products.Application.Dtos;

public static class ProductSalesTrend
{
    public const int Months = 6;

    /// <summary>The first day (UTC) of the oldest month in the trend: five months before the current one.</summary>
    public static DateTimeOffset FirstMonth(DateTimeOffset now) =>
        new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-(Months - 1));
}
