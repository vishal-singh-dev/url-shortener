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

    private static ShortUrlService CreateService(IClock? clock = null)
    {
        return new ShortUrlService(
            new InMemoryShortLinkRepository(),
            new InMemoryShortCodeGenerator(),
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
