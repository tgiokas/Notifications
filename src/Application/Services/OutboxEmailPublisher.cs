using Microsoft.Extensions.Logging;

using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;

namespace Notifications.Application.Services;

/// IEmailPublisher implementation for the non-Kafka delivery mode: writes the
/// email to a durable outbox store instead of publishing to a topic.
/// OutboxDispatcher (Infrastructure) polls the store and drives delivery
/// through IEmailSender, mirroring what KafkaEmailConsumer does for the
/// Kafka mode.
public class OutboxEmailPublisher : IEmailPublisher
{
    private readonly IOutboxStore _store;
    private readonly ILogger<OutboxEmailPublisher> _logger;

    public OutboxEmailPublisher(IOutboxStore store, ILogger<OutboxEmailPublisher> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task PublishAsync(NotificationEmailDto emailDto, CancellationToken cancellationToken = default)
    {
        await _store.EnqueueAsync(emailDto, cancellationToken);

        var recipient = emailDto.GetAllToRecipients().FirstOrDefault() ?? emailDto.Recipient;
        _logger.LogInformation("Email queued to outbox for {Recipient}", recipient);
    }
}
