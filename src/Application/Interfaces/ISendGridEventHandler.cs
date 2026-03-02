using Notifications.Application.Dtos;

namespace Notifications.Application.Interfaces;

public interface ISendGridEventHandler
{
    Task HandleEventsAsync(IReadOnlyList<SendGridEventDto> events, CancellationToken cancellationToken = default);
}
