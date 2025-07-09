using System.Threading;

namespace Notifications.Application.Interfaces;

public interface _IRabbitMqPublisher
{
    Task PublishMessageAsync(string queueName, string message, CancellationToken cancellationToken = default);
}
