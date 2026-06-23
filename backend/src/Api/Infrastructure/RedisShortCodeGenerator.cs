using Api.Contracts;
using StackExchange.Redis;

namespace Api.Infrastructure;

public sealed class RedisShortCodeGenerator : IShortCodeGenerator
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string CounterKey = "short-links:counter";
    private readonly IDatabase _database;

    public RedisShortCodeGenerator(IConnectionMultiplexer redis)
    {
        _database = redis.GetDatabase();
    }

    public async Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        var next = await _database.StringIncrementAsync(CounterKey);
        return ToBase62(next);
    }

    private static string ToBase62(long value)
    {
        if (value == 0)
        {
            return "0";
        }

        Span<char> buffer = stackalloc char[11];
        var index = buffer.Length;

        while (value > 0)
        {
            buffer[--index] = Alphabet[(int)(value % Alphabet.Length)];
            value /= Alphabet.Length;
        }

        return new string(buffer[index..]);
    }
}
