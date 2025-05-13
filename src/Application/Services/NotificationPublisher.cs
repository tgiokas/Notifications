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
        // Create domain entity and save it with Pending status
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

        // Determine routing key based on channel
        var routingKey = request.Channel.ToLower() switch
        {
            "email" => "email",
            "sms" => "sms",
            _ => throw new ArgumentOutOfRangeException(nameof(request.Channel), $"Unsupported channel: {request.Channel}")
        };

        var message = JsonSerializer.Serialize(request);

        try
        {
            // Will publish to exchange: "notifications"
            // With routing key: "email"
            // Routed to queue: "notifications.email"
            await _rabbitMqPublisher.PublishMessageAsync(routingKey, message, cancellationToken);

            // Update the entity as Sent after successful publish
            //notification.Status = NotificationStatus.Sent.ToString();
            //notification.SentAt = DateTime.UtcNow;

            //await _notificationRepository.UpdateAsync(notification);
        }
        catch (Exception)
        {
            // You could also update status to Failed here or implement retry logic
            //notification.Status = NotificationStatus.Failed.ToString();
            //await _notificationRepository.UpdateAsync(notification);

            throw;
        }
    }
}
