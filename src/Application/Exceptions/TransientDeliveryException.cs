namespace Notifications.Application.Exceptions;

public sealed class TransientDeliveryException : Exception
{
    public TransientDeliveryException(string message, Exception inner) 
        : base(message, inner) { }
}