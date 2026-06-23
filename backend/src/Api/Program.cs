using Api.Infrastructure;
using Api.Services;
using Amazon.Lambda.AspNetCoreServer;

var builder = WebApplication.CreateBuilder(args);

const string corsPolicy = "Frontend";

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicy, policy =>
    {
        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();

            return;
        }

        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ShortUrlService>();
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(corsPolicy);

app.MapGet("/", () => Results.Ok(new
{
    name = "Url Shortener API",
    status = "ok"
}))
    .WithName("HealthCheck");

app.MapPost("/api/urls", async (
    CreateShortUrlRequest request,
    ShortUrlService shortUrls,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var result = await shortUrls.CreateAsync(
        request,
        GetShortUrlBase(httpContext),
        cancellationToken);

    return result.Status switch
    {
        CreateShortUrlStatus.Created => Results.Created($"/api/urls/{result.Link!.Code}", result.Link),
        CreateShortUrlStatus.Invalid => Results.BadRequest(new { error = result.Error }),
        CreateShortUrlStatus.Conflict => Results.Conflict(new { error = result.Error }),
        _ => Results.Problem("Unexpected short URL creation result.")
    };
})
    .WithName("CreateShortUrl");

app.MapGet("/api/urls/recent", async (
    int? limit,
    ShortUrlService shortUrls,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var links = await shortUrls.GetRecentAsync(
        limit ?? 20,
        GetShortUrlBase(httpContext),
        cancellationToken);

    return Results.Ok(links);
})
    .WithName("GetRecentShortUrls");

app.MapGet("/api/urls/{code}/stats", async (
    string code,
    ShortUrlService shortUrls,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var stats = await shortUrls.GetStatsAsync(
        code,
        GetShortUrlBase(httpContext),
        cancellationToken);

    return stats is null
        ? Results.NotFound(new { error = "Short URL was not found." })
        : Results.Ok(stats);
})
    .WithName("GetShortUrlStats");

app.MapGet("/{code}", async (
    string code,
    ShortUrlService shortUrls,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var result = await shortUrls.GetRedirectAsync(
        code,
        httpContext.Request.Headers.Referer.ToString(),
        httpContext.Request.Headers.UserAgent.ToString(),
        cancellationToken);

    return result.Status switch
    {
        RedirectLookupStatus.Found => Results.Redirect(result.LongUrl!, permanent: false),
        RedirectLookupStatus.Expired => Results.StatusCode(StatusCodes.Status410Gone),
        RedirectLookupStatus.NotFound => Results.NotFound(new { error = "Short URL was not found." }),
        _ => Results.Problem("Unexpected redirect lookup result.")
    };
})
    .WithName("RedirectShortUrl");

app.Run();

static string GetShortUrlBase(HttpContext httpContext)
{
    var configuredBaseUrl = httpContext.RequestServices
        .GetRequiredService<IConfiguration>()
        ["ShortUrls:BaseUrl"];

    if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
    {
        return configuredBaseUrl;
    }

    return $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
}

public partial class Program;
