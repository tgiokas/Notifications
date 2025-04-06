using System.Threading;

namespace Notifications.Application.Interfaces;

public interface IRabbitMqPublisher
{
    Task PublishMessageAsync(string queueName, string message, CancellationToken cancellationToken = default);
}
