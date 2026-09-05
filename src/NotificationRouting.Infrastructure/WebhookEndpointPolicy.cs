using Microsoft.Extensions.Options;
using NotificationRouting.Application.Abstractions;

namespace NotificationRouting.Infrastructure;

public sealed class WebhookEndpointPolicy : IWebhookEndpointPolicy
{
    private readonly HashSet<string> _allowedHosts;
    private readonly bool _allowHttp;

    public WebhookEndpointPolicy(IOptions<WebhookOptions> options)
    {
        _allowHttp = options.Value.AllowHttp;
        _allowedHosts = new HashSet<string>(options.Value.AllowedHosts, StringComparer.OrdinalIgnoreCase);
    }

    public Uri Validate(string endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri))
            throw new ArgumentException("Webhook URL must be an absolute URI.", nameof(endpoint));

        bool validScheme = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || (_allowHttp && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase));
        if (!validScheme)
            throw new ArgumentException("Webhook URL must use HTTPS.", nameof(endpoint));

        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("Webhook URL cannot contain user information.", nameof(endpoint));

        if (_allowedHosts.Count == 0 || !_allowedHosts.Contains(uri.IdnHost))
            throw new ArgumentException("Webhook host is not allowed.", nameof(endpoint));

        return uri;
    }
}
