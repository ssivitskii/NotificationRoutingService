using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using NotificationRouting.Application;

namespace NotificationRouting.Api;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ApiExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        (int status, string title) = exception switch
        {
            NotificationNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            IdempotencyConflictException => (StatusCodes.Status409Conflict, "Idempotency conflict"),
            DeliveryStateConflictException => (StatusCodes.Status409Conflict, "Delivery state conflict"),
            NotificationConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            DeliveryQueueUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Delivery queue unavailable"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error"),
        };

        if (status >= 500)
            _logger.LogError(exception, "Unhandled request failure");
        else
            _logger.LogInformation("Request rejected with status {StatusCode}: {Reason}", status, exception.Message);

        httpContext.Response.StatusCode = status;
        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status >= 500 ? "An unexpected error occurred." : exception.Message,
                Instance = httpContext.Request.Path,
            },
        }).ConfigureAwait(false);
    }
}
