namespace Api.Services;

public sealed record LinkStatsResponse(
    string Code,
    string ShortUrl,
    string LongUrl,
    long ClickCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    IReadOnlyList<RecentClickResponse> RecentClicks);

public sealed record RecentClickResponse(
    DateTimeOffset OccurredAtUtc,
    string? Referrer,
    string? UserAgent);
