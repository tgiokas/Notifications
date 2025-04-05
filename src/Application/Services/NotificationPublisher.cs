using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using System.Text.Json;

namespace Notifications.Application.Services;

public class NotificationPublisher : INotificationPublisher
{
    private readonly IRabbitMqPublisher _rabbitMqPublisher;

    public NotificationPublisher(IRabbitMqPublisher rabbitMqPublisher)
    {
        _rabbitMqPublisher = rabbitMqPublisher;
    }

    public async Task PublishAsync(NotificationRequestDto request, CancellationToken cancellationToken = default)
    {
        var queueName = request.Channel.ToLower() switch
        {
            "email" => "notifications.email",
            "sms" => "notifications.sms",
            _ => throw new ArgumentOutOfRangeException(nameof(request.Channel), $"Unsupported channel: {request.Channel}")
        };

        var message = JsonSerializer.Serialize(request);
        await _rabbitMqPublisher.PublishMessageAsync(queueName, message);
    }
}

