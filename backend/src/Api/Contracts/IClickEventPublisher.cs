using Api.Models;

namespace Api.Contracts;

public interface IClickEventPublisher
{
    Task PublishAsync(ClickEvent clickEvent, CancellationToken cancellationToken);
}
