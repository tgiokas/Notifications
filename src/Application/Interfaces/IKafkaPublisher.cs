namespace Notifications.Application.Interfaces;

public interface IKafkaPublisher
{
    Task PublishMessageAsync(string topic, string message, CancellationToken cancellationToken = default);
}