using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Notifications.Application.Services.Channels;

public class EmailChannelPublisher : INotificationChannelPublisher
{
    public string Channel => "email";

    private readonly IMessagePublisher _kafka;
    private readonly ILogger<EmailChannelPublisher> _logger;

    public EmailChannelPublisher(IMessagePublisher kafka, ILogger<EmailChannelPublisher> logger)
    {
        _kafka = kafka;
        _logger = logger;
    }

    public async Task PublishAsync(NotificationRequestDto request, CancellationToken cancellationToken = default)
    {
        var envelope = new KafkaMessage<NotificationRequestDto>
        {
            Id = Guid.NewGuid().ToString("N"),
            Content = request,
            Timestamp = DateTime.UtcNow
        };

        var headers = new[]
        {
            new KeyValuePair<string,string>("content-type", "application/json"),
            new KeyValuePair<string,string>("x-channel", Channel),
            new KeyValuePair<string,string>("x-message-id", envelope.Id)
        };

        var key = request.Recipient ?? envelope.Id;
        await _kafka.PublishJsonAsync(route: "email", key: key, payload: envelope, headers, cancellationToken);
    }
}
