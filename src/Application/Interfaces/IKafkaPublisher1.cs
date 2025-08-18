namespace Notifications.Application.Interfaces;

public interface IKafkaPublisher1
{
    Task PublishMessageAsync(string topic, string key, string message, CancellationToken cancellationToken = default);
}