namespace NotificationRouting.Application.Abstractions;

public interface IWebhookEndpointPolicy
{
    Uri Validate(string endpoint);
}
