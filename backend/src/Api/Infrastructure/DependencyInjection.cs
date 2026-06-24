using Api.Contracts;
using Api.Database;
using StackExchange.Redis;

namespace Api.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();

        if (UseInMemoryDatabase(configuration))
        {
            services.AddSingleton<IShortLinkRepository, InMemoryShortLinkRepository>();
        }
        else if (HasDatabaseConnectionString(configuration))
        {
            services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
            services.AddSingleton<DatabaseInitializer>();
            services.AddSingleton<IShortLinkRepository, NpgsqlShortLinkRepository>();
        }
        else
        {
            services.AddSingleton<IShortLinkRepository, InMemoryShortLinkRepository>();
        }

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

    private static bool HasDatabaseConnectionString(IConfiguration configuration)
    {
        return !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Default"));
    }

    private static bool UseInMemoryDatabase(IConfiguration configuration)
    {
        return configuration.GetValue<bool>("Database:UseInMemory");
    }
}
