namespace Api.Services;

public sealed record ShortLinkResponse(
    string Code,
    string ShortUrl,
    string LongUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    long ClickCount);
