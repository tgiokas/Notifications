using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain.Interfaces;
using Notifications.Infrastructure.Messaging;

public class NotificationPublisher2 : INotificationPublisher
{
    private readonly KafkaEmailPublisher1 _emailPublisher;
    //private readonly ISmsKafkaPublisher _smsPublisher;
    private readonly INotificationRepository _notificationRepository;

    public NotificationPublisher2(
        KafkaEmailPublisher1 emailPublisher,

        INotificationRepository notificationRepository)
    {
        _emailPublisher = emailPublisher;
        _notificationRepository = notificationRepository;
    }

    public async Task PublishAsync(NotificationRequestDto request, CancellationToken cancellationToken = default)
    {
        // Create a domain entity and persist it if needed
        // var notification = new Notification { ... };
        // await _notificationRepository.AddAsync(notification);

        switch (request.Channel.ToLowerInvariant())
        {
            case "email":
                var email = new EmailNotificationDto
                {
                    To = request.Recipient,
                    Subject = request.Subject,
                    Body = request.Message
                };
                await _emailPublisher.PublishAsync("email", email, cancellationToken);
                break;

            case "sms":
                //var sms = new SmsNotificationDto
                //{
                //    PhoneNumber = request.Recipient,
                //    Message = request.Message
                //};
                //await _smsPublisher.PublishAsync("sms", sms, cancellationToken);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(request.Channel), $"Unsupported channel: {request.Channel}");
        }

        // Optionally update the notification as Sent
        // notification.Status = NotificationStatus.Sent.ToString();
        // await _notificationRepository.UpdateAsync(notification);
    }
}
