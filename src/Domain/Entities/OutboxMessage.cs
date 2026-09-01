namespace Notifications.Domain.Entities;

/// Outbox Pattern: the REST email endpoint writes a row here instead of
/// sending inline or publishing to Kafka. OutboxProcessor picks up pending
/// rows and drives them through IEmailSender. If the sender is down or the
/// service restarts, unprocessed messages are simply retried.
public class OutboxMessage
{
    public int Id { get; set; }
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;     // e.g. "email.send"
    public string Payload { get; set; } = string.Empty;       // JSON-serialized NotificationEmailDto
    public string? Key { get; set; }                          // primary recipient, for correlation
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }                // null = pending, set = sent
    public DateTime? LastAttemptAt { get; set; }               // when the last send attempt was made
    public int RetryCount { get; set; } = 0;
    public string? Error { get; set; }                         // last error, if any
}
