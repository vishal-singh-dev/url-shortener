namespace Api.Services;

public sealed record CreateShortUrlRequest(
    string LongUrl,
    string? CustomAlias,
    DateTimeOffset? ExpiresAtUtc);
