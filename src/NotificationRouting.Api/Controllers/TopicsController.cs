using Microsoft.AspNetCore.Mvc;
using NotificationRouting.Api.Contracts;
using NotificationRouting.Application;
using NotificationRouting.Application.Abstractions;
using NotificationRouting.Domain;

namespace NotificationRouting.Api.Controllers;

[ApiController]
[Route("api/topics")]
public sealed class TopicsController : ControllerBase
{
    private readonly INotificationService _service;

    public TopicsController(INotificationService service)
    {
        _service = service;
    }

    [HttpPost]
    [ProducesResponseType<TopicResponse>(StatusCodes.Status201Created)]
    public ActionResult<TopicResponse> Create(CreateTopicRequest request)
    {
        Topic topic = _service.CreateTopic(request.Name);
        return StatusCode(StatusCodes.Status201Created, TopicResponse.FromDomain(topic));
    }

    [HttpPost("{topicId:guid}/subscribers")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Subscribe(Guid topicId, SubscribeRequest request)
    {
        _service.Subscribe(topicId, request.UserId, request.MinimumImportance);
        return NoContent();
    }

    [HttpPost("{topicId:guid}/messages")]
    [ProducesResponseType<PublishAcceptedResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PublishAcceptedResponse>> Publish(
        Guid topicId,
        PublishMessageRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128)
        {
            ModelState.AddModelError("Idempotency-Key", "A non-empty Idempotency-Key of at most 128 characters is required.");
            return ValidationProblem(ModelState);
        }

        PublishReceipt receipt = await _service.PublishAsync(
            topicId,
            request.Title,
            request.Body,
            request.Importance,
            idempotencyKey,
            cancellationToken).ConfigureAwait(false);
        if (receipt.IsReplay)
            Response.Headers.Append("Idempotency-Replayed", "true");

        var response = new PublishAcceptedResponse(receipt.MessageId, receipt.IsReplay);
        return Accepted($"/api/deliveries/{receipt.MessageId}", response);
    }
}
