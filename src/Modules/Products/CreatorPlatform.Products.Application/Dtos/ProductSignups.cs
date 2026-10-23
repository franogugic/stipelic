namespace CreatorPlatform.Products.Application.Dtos;

/// <param name="Trend">Sign-ups per local calendar month, oldest first, the current month last
/// (<see cref="ProductSalesTrend.Months"/> entries).</param>
public sealed record ProductSignups(int Total, int[] Trend);
