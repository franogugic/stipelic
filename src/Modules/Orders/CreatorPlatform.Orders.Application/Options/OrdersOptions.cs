namespace CreatorPlatform.Orders.Application.Options;

public sealed class OrdersOptions
{
    public const string SectionName = "Orders";

    public string FrontendBaseUrl { get; init; } = string.Empty;

    public string ApiBaseUrl { get; init; } = string.Empty;
}
