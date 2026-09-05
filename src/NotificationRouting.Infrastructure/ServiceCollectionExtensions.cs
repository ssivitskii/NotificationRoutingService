using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationRouting.Application;
using NotificationRouting.Application.Abstractions;
using NotificationRouting.Domain.Abstractions;

namespace NotificationRouting.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationRouting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        DeliveryOptions deliveryOptions = configuration.GetSection(DeliveryOptions.SectionName).Get<DeliveryOptions>()
            ?? new DeliveryOptions();
        Validate(deliveryOptions);

        services.AddOptions<WebhookOptions>()
            .Bind(configuration.GetSection(WebhookOptions.SectionName));
        services.AddSingleton(deliveryOptions);
        services.AddSingleton<IUserStore, InMemoryUserStore>();
        services.AddSingleton<ITopicStore, InMemoryTopicStore>();
        services.AddSingleton<IMessageArchive, InMemoryMessageArchive>();
        services.AddSingleton<IAlertSink, LoggerAlertSink>();
        services.AddSingleton<IDeliveryLog, LoggerDeliveryLog>();
        services.AddSingleton<IDeliveryDelay, SystemDeliveryDelay>();
        services.AddSingleton<IDeliveryQueue, ChannelDeliveryQueue>();
        services.AddSingleton<IDeliveryStore, InMemoryDeliveryStore>();
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        services.AddSingleton<IWebhookEndpointPolicy, WebhookEndpointPolicy>();
        services.AddSingleton<IWebhookSink, HttpWebhookSink>();
        services.AddSingleton<IMessageFormatter, MarkdownMessageFormatter>();
        services.AddSingleton<DeliveryProcessor>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddHostedService<NotificationDeliveryWorker>();
        services.AddHttpClient(HttpWebhookSink.ClientName, client =>
            client.Timeout = TimeSpan.FromSeconds(deliveryOptions.WebhookTimeoutSeconds))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        return services;
    }

    private static void Validate(DeliveryOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.ChannelCapacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxAutomaticAttempts);
        if (options.MaxAutomaticAttempts > 10)
            throw new ArgumentOutOfRangeException(nameof(options), "At most 10 automatic delivery attempts are supported.");

        ArgumentOutOfRangeException.ThrowIfNegative(options.BaseRetryDelayMilliseconds);
        if (options.BaseRetryDelayMilliseconds > 60000)
            throw new ArgumentOutOfRangeException(nameof(options), "Base retry delay cannot exceed 60000 milliseconds.");

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.WebhookTimeoutSeconds);
    }
}
