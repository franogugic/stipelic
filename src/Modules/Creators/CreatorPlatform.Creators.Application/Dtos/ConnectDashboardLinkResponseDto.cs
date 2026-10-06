namespace CreatorPlatform.Creators.Application.Dtos;

/// <summary>Opens the creator's Stripe Express dashboard. Single-use and short-lived: open it right away.</summary>
public sealed record ConnectDashboardLinkResponseDto(string Url);
