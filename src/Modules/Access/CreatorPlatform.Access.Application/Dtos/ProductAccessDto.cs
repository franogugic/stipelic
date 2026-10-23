namespace CreatorPlatform.Access.Application.Dtos;

/// <summary>The public download page (<c>/d/{token}</c>): what the link gives and whose it is. No internal ids.</summary>
/// <param name="Source">"order" (a purchase) or "capture" (a lead page sign-up).</param>
/// <param name="OrderNumber">Purchases only: the reference from the order email.</param>
public sealed record ProductAccessDto(
    string ProductName,
    string FileName,
    long FileSizeBytes,
    ProductAccessBrandDto Brand,
    string Source,
    string? OrderNumber,
    DateTimeOffset? PaidAt);

/// <param name="Name">The creator's brand name, else the workspace name.</param>
public sealed record ProductAccessBrandDto(string Name, string? Colour, string? LogoUrl);

/// <summary>A short-lived signed URL for the file; the browser downloads it as an attachment.</summary>
public sealed record ProductDownloadDto(string Url);
