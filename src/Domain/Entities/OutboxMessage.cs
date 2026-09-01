namespace Notifications.Domain.Entities;

public enum OutboxMessageStatus
{
    Pending = 0,
    Processing = 1,
    Sent = 2,
    Dead = 3
}

/// A durably-stored message awaiting delivery, used as the non-Kafka
/// alternative to publishing onto a topic. OutboxDispatcher polls for
/// Pending rows and drives them through IEmailSender.
public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Channel { get; set; } = "email";
    public string Payload { get; set; } = string.Empty;
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
