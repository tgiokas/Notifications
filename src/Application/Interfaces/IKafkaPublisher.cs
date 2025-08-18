using Notifications.Application.DTOs;

namespace Notifications.Application.Interfaces;

public interface IKafkaPublisher<TKey, TMessage> : IDisposable
     where TKey : class
     where TMessage : class
{
    Task PublishMessageAsync(string topic, TKey? key, KafkaMessage<TMessage> message, CancellationToken cancellationToken = default);
}