using Notifications.Application.DTOs;

namespace Notifications.Infrastructure.Messaging;

public class MessageStoreService<TMessage>
    where TMessage : class
{
    private readonly List<KafkaMessage<TMessage>> _messages = new();

    public void AddMessage(KafkaMessage<TMessage> message)
    {
        try
        {
            _messages.Add(message);

            // Keep only the last 100 messages
            if (_messages.Count > 100)
            {
                _messages.RemoveAt(0);
            }
        }
        finally
        {
        }
    }

    public List<KafkaMessage<TMessage>> GetMessages()
    {
        try
        {
            return _messages.ToList();
        }
        finally
        {
        }
    }
}
