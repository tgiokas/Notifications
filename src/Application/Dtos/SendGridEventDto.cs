using System.Text.Json.Serialization;

namespace Notifications.Application.Dtos;

/// <summary>
/// Represents a single event from the SendGrid Event Webhook.
/// SendGrid posts an array of these to your webhook URL.
/// See: https://docs.sendgrid.com/for-developers/tracking-events/event
/// </summary>
public class SendGridEventDto
{
    /// <summary>Recipient email address.</summary>
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Event type: processed, dropped, delivered, deferred, bounce,
    /// open, click, spamreport, unsubscribe, group_unsubscribe, group_resubscribe.
    /// </summary>
    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    /// <summary>Unix timestamp of the event.</summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    /// <summary>Unique ID for this event (use for idempotency).</summary>
    [JsonPropertyName("sg_event_id")]
    public string? SgEventId { get; set; }

    /// <summary>SendGrid internal message ID.</summary>
    [JsonPropertyName("sg_message_id")]
    public string? SgMessageId { get; set; }

    /// <summary>SMTP message ID.</summary>
    [JsonPropertyName("smtp-id")]
    public string? SmtpId { get; set; }

    /// <summary>Reason for bounce/drop/deferred.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>Bounce type: bounce (permanent) or blocked (temporary).</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>SMTP status code (e.g. "550", "250").</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>Attempt number for deferred messages.</summary>
    [JsonPropertyName("attempt")]
    public int? Attempt { get; set; }

    /// <summary>URL clicked (for click events).</summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>User agent string (for open/click events).</summary>
    [JsonPropertyName("useragent")]
    public string? UserAgent { get; set; }

    /// <summary>IP address of the recipient (for open/click events).</summary>
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    /// <summary>Categories assigned to the message.</summary>
    [JsonPropertyName("category")]
    public List<string>? Category { get; set; }

    /// <summary>Helper to convert Unix timestamp to DateTime.</summary>
    public DateTime EventTimestamp => DateTimeOffset.FromUnixTimeSeconds(Timestamp).UtcDateTime;
}
