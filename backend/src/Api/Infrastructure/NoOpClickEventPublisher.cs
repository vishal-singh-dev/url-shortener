using Api.Contracts;
using Api.Models;

namespace Api.Infrastructure;

public sealed class NoOpClickEventPublisher : IClickEventPublisher
{
    public Task PublishAsync(ClickEvent clickEvent, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
