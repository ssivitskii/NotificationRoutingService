namespace NotificationRouting.Api;

public static class RequestBodyLimitExtensions
{
    public const long MaxRequestBodySize = 128 * 1024;
    public const string PayloadTooLargeDetail = "The request body must not exceed 128 KiB.";

    public static void ConfigureRequestBodyLimit(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.WebHost.ConfigureKestrel(options =>
            options.Limits.MaxRequestBodySize = MaxRequestBodySize);
    }

    public static void UseRequestBodyLimit(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Use(async (context, next) =>
        {
            if (context.Request.ContentLength > MaxRequestBodySize)
            {
                throw new BadHttpRequestException(
                    PayloadTooLargeDetail,
                    StatusCodes.Status413PayloadTooLarge);
            }

            await next(context).ConfigureAwait(false);
        });
    }
}
