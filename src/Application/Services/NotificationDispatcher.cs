using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Application.Services;

public class NotificationDispatcher : INotificationDispatcher
{
    private readonly IReadOnlyDictionary<string, INotificationChannelPublisher> _byChannel;

    public NotificationDispatcher(IEnumerable<INotificationChannelPublisher> strategies)
    {
        _byChannel = strategies.ToDictionary(s => s.Channel, StringComparer.OrdinalIgnoreCase);
    }

    public Task DispatchAsync(NotificationRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.Channel))
            throw new ArgumentException("Channel is required", nameof(request));

        if (!_byChannel.TryGetValue(request.Channel.Trim(), out var strategy))
            throw new ArgumentOutOfRangeException(nameof(request.Channel), $"Unsupported channel '{request.Channel}'.");

        return strategy.PublishAsync(request, cancellationToken);
    }
}
