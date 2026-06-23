using Api.Contracts;
using Api.Infrastructure;
using Api.Models;
using Api.Services;

namespace Api.Tests.Unit;

public sealed class ShortUrlServiceTests
{
    [Fact]
    public async Task CreateAsync_WithInvalidUrl_ReturnsInvalid()
    {
        var service = CreateService();

        var result = await service.CreateAsync(
            new CreateShortUrlRequest("not-a-url", null, null),
            "https://sho.rt",
            CancellationToken.None);

        Assert.Equal(CreateShortUrlStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task CreateAsync_WithCustomAlias_CreatesExpectedShortUrl()
    {
        var service = CreateService();

        var result = await service.CreateAsync(
            new CreateShortUrlRequest("https://example.com/products", "products", null),
            "https://sho.rt",
            CancellationToken.None);

        Assert.Equal(CreateShortUrlStatus.Created, result.Status);
        Assert.Equal("products", result.Link!.Code);
        Assert.Equal("https://sho.rt/products", result.Link.ShortUrl);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateCustomAlias_ReturnsConflict()
    {
        var service = CreateService();

        await service.CreateAsync(
            new CreateShortUrlRequest("https://example.com/one", "promo", null),
            "https://sho.rt",
            CancellationToken.None);

        var duplicate = await service.CreateAsync(
            new CreateShortUrlRequest("https://example.com/two", "promo", null),
            "https://sho.rt",
            CancellationToken.None);

        Assert.Equal(CreateShortUrlStatus.Conflict, duplicate.Status);
    }

    [Fact]
    public async Task GetRedirectAsync_ForExpiredLink_ReturnsExpired()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var service = CreateService(clock);

        var created = await service.CreateAsync(
            new CreateShortUrlRequest(
                "https://example.com",
                "expired",
                clock.UtcNow.AddMinutes(1)),
            "https://sho.rt",
            CancellationToken.None);

        clock.UtcNow = clock.UtcNow.AddMinutes(2);

        var redirect = await service.GetRedirectAsync(created.Link!.Code, null, null, CancellationToken.None);

        Assert.Equal(RedirectLookupStatus.Expired, redirect.Status);
    }

    [Fact]
    public async Task GetRedirectAsync_ForActiveLink_TracksClick()
    {
        var service = CreateService();

        var created = await service.CreateAsync(
            new CreateShortUrlRequest("https://example.com", "active", null),
            "https://sho.rt",
            CancellationToken.None);

        var redirect = await service.GetRedirectAsync(
            created.Link!.Code,
            "https://referrer.example",
            "test-agent",
            CancellationToken.None);

        var stats = await service.GetStatsAsync(created.Link.Code, "https://sho.rt", CancellationToken.None);

        Assert.Equal(RedirectLookupStatus.Found, redirect.Status);
        Assert.Equal("https://example.com", redirect.LongUrl);
        Assert.Equal(1, stats!.ClickCount);
        Assert.Single(stats.RecentClicks);
    }

    private static ShortUrlService CreateService(IClock? clock = null)
    {
        return new ShortUrlService(
            new InMemoryShortLinkRepository(),
            new InMemoryShortCodeGenerator(),
            new NoOpClickEventPublisher(),
            clock ?? new TestClock(DateTimeOffset.UtcNow));
    }

    private sealed class TestClock : IClock
    {
        public TestClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; set; }
    }
}
