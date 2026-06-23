using System.Text.RegularExpressions;
using Api.Contracts;
using Api.Models;

namespace Api.Services;

public sealed partial class ShortUrlService
{
    private const int MaxGeneratedCodeAttempts = 5;
    private readonly IShortLinkRepository _links;
    private readonly IShortCodeGenerator _codes;
    private readonly IClickEventPublisher _clickEvents;
    private readonly IClock _clock;

    public ShortUrlService(
        IShortLinkRepository links,
        IShortCodeGenerator codes,
        IClickEventPublisher clickEvents,
        IClock clock)
    {
        _links = links;
        _codes = codes;
        _clickEvents = clickEvents;
        _clock = clock;
    }

    public async Task<CreateShortUrlResult> CreateAsync(
        CreateShortUrlRequest request,
        string shortUrlBase,
        CancellationToken cancellationToken)
    {
        if (!IsValidHttpUrl(request.LongUrl))
        {
            return CreateShortUrlResult.Invalid("longUrl must be an absolute HTTP or HTTPS URL.");
        }

        if (request.ExpiresAtUtc is not null && request.ExpiresAtUtc <= _clock.UtcNow)
        {
            return CreateShortUrlResult.Invalid("expiresAtUtc must be in the future.");
        }

        var hasCustomAlias = !string.IsNullOrWhiteSpace(request.CustomAlias);
        var code = hasCustomAlias ? request.CustomAlias!.Trim() : await _codes.GenerateAsync(cancellationToken);

        if (hasCustomAlias && !AliasRegex().IsMatch(code))
        {
            return CreateShortUrlResult.Invalid("customAlias must be 3-32 characters and use only letters, numbers, underscores, or hyphens.");
        }

        for (var attempt = 0; attempt < MaxGeneratedCodeAttempts; attempt++)
        {
            var link = new ShortLink(
                code,
                request.LongUrl.Trim(),
                _clock.UtcNow,
                request.ExpiresAtUtc,
                hasCustomAlias);

            if (await _links.CreateAsync(link, cancellationToken))
            {
                return CreateShortUrlResult.Created(ToResponse(link, shortUrlBase));
            }

            if (hasCustomAlias)
            {
                return CreateShortUrlResult.Conflict("customAlias is already in use.");
            }

            code = await _codes.GenerateAsync(cancellationToken);
        }

        return CreateShortUrlResult.Conflict("Could not generate a unique short code.");
    }

    public async Task<RedirectLookupResult> GetRedirectAsync(
        string code,
        string? referrer,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var link = await _links.GetByCodeAsync(code, cancellationToken);

        if (link is null)
        {
            return new RedirectLookupResult(RedirectLookupStatus.NotFound, null);
        }

        if (link.IsExpired(_clock.UtcNow))
        {
            return new RedirectLookupResult(RedirectLookupStatus.Expired, null);
        }

        var clickEvent = new ClickEvent(code, _clock.UtcNow, referrer, userAgent);
        await _links.RegisterClickAsync(clickEvent, cancellationToken);

        try
        {
            await _clickEvents.PublishAsync(clickEvent, cancellationToken);
        }
        catch
        {
            // Redirects should keep working even when async analytics transport is unavailable.
        }

        return new RedirectLookupResult(RedirectLookupStatus.Found, link.LongUrl);
    }

    public async Task<IReadOnlyList<ShortLinkResponse>> GetRecentAsync(
        int limit,
        string shortUrlBase,
        CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        var links = await _links.GetRecentAsync(safeLimit, cancellationToken);

        return links.Select(link => ToResponse(link, shortUrlBase)).ToArray();
    }

    public async Task<LinkStatsResponse?> GetStatsAsync(
        string code,
        string shortUrlBase,
        CancellationToken cancellationToken)
    {
        var link = await _links.GetByCodeAsync(code, cancellationToken);

        if (link is null)
        {
            return null;
        }

        var clicks = await _links.GetRecentClicksAsync(code, 25, cancellationToken);

        return new LinkStatsResponse(
            link.Code,
            BuildShortUrl(shortUrlBase, link.Code),
            link.LongUrl,
            link.ClickCount,
            link.CreatedAtUtc,
            link.ExpiresAtUtc,
            clicks.Select(click => new RecentClickResponse(click.OccurredAtUtc, click.Referrer, click.UserAgent)).ToArray());
    }

    private static ShortLinkResponse ToResponse(ShortLink link, string shortUrlBase)
    {
        return new ShortLinkResponse(
            link.Code,
            BuildShortUrl(shortUrlBase, link.Code),
            link.LongUrl,
            link.CreatedAtUtc,
            link.ExpiresAtUtc,
            link.ClickCount);
    }

    private static string BuildShortUrl(string shortUrlBase, string code)
    {
        return $"{shortUrlBase.TrimEnd('/')}/{code}";
    }

    private static bool IsValidHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static Regex AliasRegex()
    {
        return new Regex("^[A-Za-z0-9_-]{3,32}$");
    }
}

public sealed record CreateShortUrlResult(
    CreateShortUrlStatus Status,
    ShortLinkResponse? Link,
    string? Error)
{
    public static CreateShortUrlResult Created(ShortLinkResponse link)
    {
        return new CreateShortUrlResult(CreateShortUrlStatus.Created, link, null);
    }

    public static CreateShortUrlResult Invalid(string error)
    {
        return new CreateShortUrlResult(CreateShortUrlStatus.Invalid, null, error);
    }

    public static CreateShortUrlResult Conflict(string error)
    {
        return new CreateShortUrlResult(CreateShortUrlStatus.Conflict, null, error);
    }
}

public enum CreateShortUrlStatus
{
    Created,
    Invalid,
    Conflict
}
