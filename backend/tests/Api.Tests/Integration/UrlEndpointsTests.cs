using System.Net;
using System.Net.Http.Json;
using Api.Contracts;
using Api.Database;
using Api.Infrastructure;
using Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Tests.Integration;

public sealed class UrlEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public UrlEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:UseInMemory"] = "true",
                    ["ConnectionStrings:Default"] = "",
                    ["Redis:ConnectionString"] = ""
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDbConnectionFactory>();
                services.RemoveAll<DatabaseInitializer>();
                services.RemoveAll<IShortLinkRepository>();
                services.AddSingleton<IShortLinkRepository, InMemoryShortLinkRepository>();
            });
        });
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
    public async Task UnknownPath_ReturnsNotFound()
    {
        var client = _factory.CreateClient();

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
