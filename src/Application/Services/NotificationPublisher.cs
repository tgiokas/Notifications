using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain.Entities;
using Notifications.Domain.Enums;
using Notifications.Domain.Interfaces;
using System.Text.Json;

namespace Notifications.Application.Services;

public class NotificationPublisher : INotificationPublisher
{
    private readonly IRabbitMqPublisher _rabbitMqPublisher;
    private readonly INotificationRepository _notificationRepository;

    public NotificationPublisher(
        IRabbitMqPublisher rabbitMqPublisher,
        INotificationRepository notificationRepository)
    {
        _rabbitMqPublisher = rabbitMqPublisher;
        _notificationRepository = notificationRepository;
    }

    public async Task PublishAsync(NotificationRequestDto request, CancellationToken cancellationToken = default)
    {

        //var notification = new Notification
        //{
        //    Id = Guid.NewGuid(),
        //    Recipient = request.Recipient,
        //    Subject = request.Subject,
        //    Message = request.Message,
        //    Channel = request.Channel,
        //    Status = NotificationStatus.Pending.ToString(),
        //    CreatedAt = DateTime.UtcNow
        //};

        //await _notificationRepository.AddAsync(notification);

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

