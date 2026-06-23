using Api.Contracts;
using StackExchange.Redis;

namespace Api.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IShortLinkRepository, InMemoryShortLinkRepository>();
        services.AddSingleton<IClickEventPublisher, NoOpClickEventPublisher>();

        var redisConnectionString = configuration["Redis:ConnectionString"];

        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddSingleton<IShortCodeGenerator, InMemoryShortCodeGenerator>();
            return services;
        }

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = RedisConnectionOptions.Parse(redisConnectionString);
            return ConnectionMultiplexer.Connect(options);
        });

        services.AddSingleton<IShortCodeGenerator, RedisShortCodeGenerator>();

        return services;
    }
}
