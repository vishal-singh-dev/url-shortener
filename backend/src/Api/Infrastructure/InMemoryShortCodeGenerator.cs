using Api.Contracts;

namespace Api.Infrastructure;

public sealed class InMemoryShortCodeGenerator : IShortCodeGenerator
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private long _counter = 100_000;

    public Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        var next = Interlocked.Increment(ref _counter);
        return Task.FromResult(ToBase62(next));
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
