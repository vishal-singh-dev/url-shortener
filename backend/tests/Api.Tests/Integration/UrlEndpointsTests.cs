using System.Net;
using System.Net.Http.Json;
using Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Integration;

public sealed class UrlEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public UrlEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostApiUrls_CreatesGeneratedShortUrl()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/urls",
            new CreateShortUrlRequest("https://example.com/articles/1", null, null));

        var created = await response.Content.ReadFromJsonAsync<ShortLinkResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(created);
        Assert.False(string.IsNullOrWhiteSpace(created.Code));
        Assert.EndsWith($"/{created.Code}", created.ShortUrl);
    }

    [Fact]
    public async Task PostApiUrls_WithDuplicateAlias_ReturnsConflict()
    {
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync(
            "/api/urls",
            new CreateShortUrlRequest("https://example.com/one", "campaign", null));

        var duplicate = await client.PostAsJsonAsync(
            "/api/urls",
            new CreateShortUrlRequest("https://example.com/two", "campaign", null));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task GetCode_ForActiveAlias_ReturnsRedirectAndUpdatesStats()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await client.PostAsJsonAsync(
            "/api/urls",
            new CreateShortUrlRequest("https://example.com/landing", "landing", null));

        var redirect = await client.GetAsync("/landing");
        var stats = await client.GetFromJsonAsync<LinkStatsResponse>("/api/urls/landing/stats");

        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
        Assert.Equal("https://example.com/landing", redirect.Headers.Location?.ToString());
        Assert.NotNull(stats);
        Assert.Equal(1, stats.ClickCount);
    }

    [Fact]
    public async Task GetCode_ForMissingCode_ReturnsNotFound()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/missing-code");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRecent_ReturnsCreatedLinks()
    {
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync(
            "/api/urls",
            new CreateShortUrlRequest("https://example.com/recent", "recent", null));

        var recent = await client.GetFromJsonAsync<IReadOnlyList<ShortLinkResponse>>("/api/urls/recent");

        Assert.NotNull(recent);
        Assert.Contains(recent, link => link.Code == "recent");
    }
}
