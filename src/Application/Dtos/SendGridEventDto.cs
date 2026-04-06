using System.Text.Json.Serialization;

namespace Notifications.Application.Dtos;

/// Represents a single event from the SendGrid Event Webhook.
/// SendGrid posts an array of these to your webhook URL.
/// See: https://docs.sendgrid.com/for-developers/tracking-events/event
public class SendGridEventDto
{
    /// Recipient email address.
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
 
    /// Event type: processed, dropped, delivered, deferred, bounce,
    /// open, click, spamreport, unsubscribe, group_unsubscribe, group_resubscribe.  
    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    /// Unix timestamp of the event.
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    /// Unique ID for this event (use for idempotency).
    [JsonPropertyName("sg_event_id")]
    public string? SgEventId { get; set; }

    /// SendGrid internal message ID.
    [JsonPropertyName("sg_message_id")]
    public string? SgMessageId { get; set; }

    /// SMTP message ID.
    [JsonPropertyName("smtp-id")]
    public string? SmtpId { get; set; }

    /// Reason for bounce/drop/deferred.
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// Bounce type: bounce (permanent) or blocked (temporary).
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// SMTP status code (e.g. "550", "250").
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// Attempt number for deferred messages.
    [JsonPropertyName("attempt")]
    public int? Attempt { get; set; }

    /// URL clicked (for click events).
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// User agent string (for open/click events).
    [JsonPropertyName("useragent")]
    public string? UserAgent { get; set; }

    /// IP address of the recipient (for open/click events).
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    /// Categories assigned to the message.
    [JsonPropertyName("category")]
    public List<string>? Category { get; set; }

    /// Helper to convert Unix timestamp to DateTime.
    public DateTime EventTimestamp => DateTimeOffset.FromUnixTimeSeconds(Timestamp).UtcDateTime;
}
