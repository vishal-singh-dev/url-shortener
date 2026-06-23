using StackExchange.Redis;

namespace Api.Infrastructure;

public static class RedisConnectionOptions
{
    public static ConfigurationOptions Parse(string connectionString)
    {
        if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri)
            && (uri.Scheme == "redis" || uri.Scheme == "rediss"))
        {
            return ParseUri(uri);
        }

        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;

        return options;
    }

    private static ConfigurationOptions ParseUri(Uri uri)
    {
        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            Ssl = uri.Scheme == "rediss"
        };

        options.EndPoints.Add(uri.Host, uri.Port);

        var userInfoParts = uri.UserInfo.Split(':', 2);

        if (userInfoParts.Length > 0 && !string.IsNullOrWhiteSpace(userInfoParts[0]))
        {
            options.User = Uri.UnescapeDataString(userInfoParts[0]);
        }

        if (userInfoParts.Length > 1 && !string.IsNullOrWhiteSpace(userInfoParts[1]))
        {
            options.Password = Uri.UnescapeDataString(userInfoParts[1]);
        }

        return options;
    }
}
