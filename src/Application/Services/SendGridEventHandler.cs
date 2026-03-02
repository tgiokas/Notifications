using Microsoft.Extensions.Logging;

using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;

namespace Notifications.Application.Services;

public class SendGridEventHandler : ISendGridEventHandler
{
    private readonly ILogger<SendGridEventHandler> _logger;

    public SendGridEventHandler(ILogger<SendGridEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleEventsAsync(IReadOnlyList<SendGridEventDto> events, CancellationToken cancellationToken = default)
    {
        foreach (var evt in events)
        {
            switch (evt.Event.ToLowerInvariant())
            {
                case "delivered":
                    _logger.LogInformation(
                        "Email delivered to {Email} at {Timestamp}. MessageId: {MessageId}",
                        evt.Email, evt.EventTimestamp, evt.SgMessageId);
                    // TODO: Update notification status to Delivered in DB
                    break;

                case "bounce":
                    _logger.LogWarning(
                        "Email bounced for {Email}. Type: {Type}, Reason: {Reason}, Status: {Status}",
                        evt.Email, evt.Type, evt.Reason, evt.Status);
                    // TODO: Update notification status to Failed, mark address as bounced
                    break;

                case "blocked":
                    _logger.LogWarning(
                        "Email blocked for {Email}. Reason: {Reason}, Status: {Status}",
                        evt.Email, evt.Reason, evt.Status);
                    // TODO: Update notification status to Blocked, Handle temporary block, maybe retry later
                    break;

                case "deferred":
                    _logger.LogWarning(
                        "Email deferred for {Email}. Attempt: {Attempt}, Reason: {Reason}",
                        evt.Email, evt.Attempt, evt.Reason);
                    break;

                case "dropped":
                    _logger.LogError(
                        "Email dropped for {Email}. Reason: {Reason}",
                        evt.Email, evt.Reason);
                    // TODO: Update notification status to Failed
                    break;

                case "processed":
                    _logger.LogInformation(
                        "Email processed for {Email}. MessageId: {MessageId}",
                        evt.Email, evt.SgMessageId);
                    break;

                case "open":
                    _logger.LogInformation(
                        "Email opened by {Email} at {Timestamp}. IP: {Ip}, UserAgent: {UserAgent}",
                        evt.Email, evt.EventTimestamp, evt.Ip, evt.UserAgent);
                    // TODO: Track open event
                    break;

                case "click":
                    _logger.LogInformation(
                        "Link clicked by {Email} at {Timestamp}. URL: {Url}",
                        evt.Email, evt.EventTimestamp, evt.Url);
                    // TODO: Track click event
                    break;

                case "spamreport":
                    _logger.LogWarning(
                        "Spam report from {Email} at {Timestamp}",
                        evt.Email, evt.EventTimestamp);                   
                    break;

                case "unsubscribe":
                    _logger.LogInformation(
                        "Unsubscribe from {Email} at {Timestamp}",
                        evt.Email, evt.EventTimestamp);                   
                    break;

                default:
                    _logger.LogDebug(
                        "Unhandled SendGrid event '{Event}' for {Email}",
                        evt.Event, evt.Email);
                    break;
            }
        }

        await Task.CompletedTask;
    }
}
