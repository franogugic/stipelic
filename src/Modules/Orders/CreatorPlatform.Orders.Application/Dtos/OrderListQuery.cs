namespace CreatorPlatform.Orders.Application.Dtos;

/// <summary>The orders list / export filters as the client sends them (R6.5).</summary>
/// <param name="Period">"today" | "7d" | … | "all": only orders whose counted date (<see cref="OrderDto.CountedAt"/>) falls in
/// the period's window; null for every order (the list is then ordered by CreatedAt, as before R6).</param>
public sealed record OrderListQuery(
    Guid? ProductId = null,
    Guid? LandingPageId = null,
    string? Status = null,
    string? Search = null,
    string? Period = null);
