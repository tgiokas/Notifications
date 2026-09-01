namespace Notifications.Application.Interfaces;

/// Generic abstraction over the message broker producer (Kafka).
public interface IMessagePublisher
{
    Task PublishJsonAsync<T>(
        string route,
        string key,
        T payload,
        IEnumerable<KeyValuePair<string, string>>? headers = null,
        CancellationToken cancellationToken = default);
}
