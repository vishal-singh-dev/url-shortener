using Api.Models;

namespace Api.Contracts;

public interface IShortLinkRepository
{
    Task<bool> CreateAsync(ShortLink link, CancellationToken cancellationToken);

    Task<ShortLink?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    Task<IReadOnlyList<ShortLink>> GetRecentAsync(int limit, CancellationToken cancellationToken);

    Task RegisterClickAsync(ClickEvent clickEvent, CancellationToken cancellationToken);

    Task<IReadOnlyList<ClickEvent>> GetRecentClicksAsync(string code, int limit, CancellationToken cancellationToken);
}
