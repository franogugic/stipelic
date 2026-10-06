namespace CreatorPlatform.Marketing.Application.Dtos;

/// <summary>What the creator-branded unsubscribe page shows. Never the recipient's email or any internal id.</summary>
public sealed record UnsubscribePageDto(UnsubscribePageCreatorDto Creator, bool AlreadyUnsubscribed);

/// <param name="Name">The creator's brand name (falls back to the workspace name).</param>
public sealed record UnsubscribePageCreatorDto(string Name, string BrandColor, string? LogoUrl, string? SupportEmail);
