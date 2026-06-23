using System.Collections.Concurrent;
using Api.Contracts;
using Api.Models;

namespace Api.Infrastructure;

public sealed class InMemoryShortLinkRepository : IShortLinkRepository
{
    private readonly ConcurrentDictionary<string, ShortLink> _links = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<ClickEvent>> _clicks = new(StringComparer.OrdinalIgnoreCase);

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

    public Task RegisterClickAsync(ClickEvent clickEvent, CancellationToken cancellationToken)
    {
        if (_links.TryGetValue(clickEvent.Code, out var link))
        {
            link.RegisterClick();

            var events = _clicks.GetOrAdd(clickEvent.Code, _ => new ConcurrentQueue<ClickEvent>());
            events.Enqueue(clickEvent);

            while (events.Count > 100 && events.TryDequeue(out _))
            {
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ClickEvent>> GetRecentClicksAsync(string code, int limit, CancellationToken cancellationToken)
    {
        if (!_clicks.TryGetValue(code, out var events))
        {
            return Task.FromResult<IReadOnlyList<ClickEvent>>(Array.Empty<ClickEvent>());
        }

        var clicks = events
            .OrderByDescending(click => click.OccurredAtUtc)
            .Take(limit)
            .ToArray();

        return Task.FromResult<IReadOnlyList<ClickEvent>>(clicks);
    }
}
