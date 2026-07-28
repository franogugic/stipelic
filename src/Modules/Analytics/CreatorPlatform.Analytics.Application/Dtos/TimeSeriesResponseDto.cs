namespace CreatorPlatform.Analytics.Application.Dtos;

public sealed record TimeSeriesPointDto(
    DateTimeOffset BucketStart,
    long ViewCount,
    long UniqueVisitors,
    long CaptureCount,
    int PurchaseCount,
    int RevenueCents);

public sealed record TimeSeriesResponseDto(
    string Period,
    string BucketUnit,
    string? Currency,
    List<TimeSeriesPointDto> Points);
