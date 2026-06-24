using System.Collections.Concurrent;
using Api.Contracts;
using Api.Models;

namespace Api.Infrastructure;

public sealed class InMemoryShortLinkRepository : IShortLinkRepository
{
    private readonly ConcurrentDictionary<string, ShortLink> _links = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> CreateAsync(ShortLink link, CancellationToken cancellationToken)
    {
        return Task.FromResult(_links.TryAdd(link.Code, link));
    }

    public Task<ShortLink?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        _links.TryGetValue(code, out var link);
        return Task.FromResult(link);
    }

    public Task<IReadOnlyList<ShortLink>> GetRecentAsync(int limit, CancellationToken cancellationToken)
    {
        var links = _links.Values
            .OrderByDescending(link => link.CreatedAtUtc)
            .Take(limit)
            .ToArray();

        return Task.FromResult<IReadOnlyList<ShortLink>>(links);
    }

}
