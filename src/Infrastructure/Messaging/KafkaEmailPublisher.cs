using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Notifications.Application.Configuration;
using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Messaging;

public class KafkaEmailPublisher : IEmailPublisher
{
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<KafkaEmailPublisher> _logger;
    private readonly string _topic;

    public KafkaEmailPublisher(
        IMessagePublisher publisher,
        IOptions<KafkaSettings> kafkaOptions,
        ILogger<KafkaEmailPublisher> logger)
    {
        _publisher = publisher;
        _logger = logger;
        _topic = kafkaOptions.Value.ProduceTopic;
    }

    public async Task PublishAsync(NotificationEmailDto emailDto, CancellationToken cancellationToken = default)
    {
        var envelope = new KafkaMessage<NotificationEmailDto>
        {
            Content = emailDto
        };

        var headers = new[]
        {
            new KeyValuePair<string, string>("content-type", "application/json"),
            new KeyValuePair<string, string>("x-channel", "email")
        };

        // Key by the first recipient so all messages to the same address land on the
        // same partition and are processed in order.
        var key = emailDto.GetAllToRecipients().FirstOrDefault() ?? emailDto.Recipient;

        await _publisher.PublishJsonAsync(_topic, key, envelope, headers, cancellationToken);

        _logger.LogInformation("Email queued to Kafka topic {Topic} for {Recipient}", _topic, key);
    }
}
