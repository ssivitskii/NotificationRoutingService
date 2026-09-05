using Microsoft.AspNetCore.Mvc;
using NotificationRouting.Api.Contracts;
using NotificationRouting.Application.Abstractions;

namespace NotificationRouting.Api.Controllers;

[ApiController]
[Route("api/deliveries")]
public sealed class DeliveriesController : ControllerBase
{
    private readonly INotificationService _service;

    public DeliveriesController(INotificationService service)
    {
        _service = service;
    }

    [HttpGet("{messageId:guid}")]
    [ProducesResponseType<DeliveryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public ActionResult<DeliveryResponse> Get(Guid messageId)
    {
        return Ok(DeliveryResponse.FromApplication(_service.GetDispatch(messageId)));
    }

    [HttpGet("dead-letter")]
    [ProducesResponseType<IReadOnlyList<DeadLetterResponse>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<DeadLetterResponse>> GetDeadLetters()
    {
        DeadLetterResponse[] response = _service.GetDeadLetters()
            .Select(DeadLetterResponse.FromApplication)
            .ToArray();
        return Ok(response);
    }

    [HttpPost("{deliveryId:guid}/retry")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Retry(Guid deliveryId, CancellationToken cancellationToken)
    {
        Guid messageId = await _service.RetryAsync(deliveryId, cancellationToken).ConfigureAwait(false);
        return Accepted($"/api/deliveries/{messageId}");
    }
}
