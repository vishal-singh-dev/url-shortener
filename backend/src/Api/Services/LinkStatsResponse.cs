namespace Api.Services;

public sealed record LinkStatsResponse(
    string Code,
    string ShortUrl,
    string LongUrl,
    long ClickCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc);
