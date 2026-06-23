namespace Api.Models;

public sealed class ShortLink
{
    public ShortLink(
        string code,
        string longUrl,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? expiresAtUtc,
        bool isCustomAlias)
    {
        Code = code;
        LongUrl = longUrl;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        IsCustomAlias = isCustomAlias;
    }

    public string Code { get; }

    public string LongUrl { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? ExpiresAtUtc { get; }

    public bool IsCustomAlias { get; }

    public long ClickCount { get; private set; }

    public bool IsExpired(DateTimeOffset nowUtc)
    {
        return ExpiresAtUtc is not null && ExpiresAtUtc <= nowUtc;
    }

    public void RegisterClick()
    {
        ClickCount++;
    }
}
