using System.Text.Json;
using Microsoft.Extensions.Logging;

using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;
using Notifications.Domain.Entities;
using Notifications.Domain.Interfaces;

namespace Notifications.Application.Services;

/// IEmailPublisher implementation for the non-Kafka delivery mode: writes the
/// email to the outbox table instead of publishing to a topic. OutboxProcessor
/// (Infrastructure) polls the table and drives delivery through IEmailSender,
/// mirroring what KafkaEmailConsumer does for the Kafka mode.
public class OutboxEmailPublisher : IEmailPublisher
{
    private const string EmailEventType = "email.send";

    private readonly IOutboxRepository _outboxRepository;
    private readonly ILogger<OutboxEmailPublisher> _logger;

    public OutboxEmailPublisher(IOutboxRepository outboxRepository, ILogger<OutboxEmailPublisher> logger)
    {
        _outboxRepository = outboxRepository;
        _logger = logger;
    }

    public async Task PublishAsync(NotificationEmailDto emailDto, CancellationToken cancellationToken = default)
    {
        var recipient = emailDto.GetAllToRecipients().FirstOrDefault() ?? emailDto.Recipient;

        var message = new OutboxMessage
        {
            EventType = EmailEventType,
            Payload = JsonSerializer.Serialize(emailDto),
            Key = recipient
        };

        await _outboxRepository.AddAsync(message);
        await _outboxRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Email queued to outbox ({EventId}) for {Recipient}", message.EventId, recipient);
    }
}
