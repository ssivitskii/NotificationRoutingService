using Microsoft.AspNetCore.Mvc;
using NotificationRouting.Api.Contracts;
using NotificationRouting.Application.Abstractions;
using NotificationRouting.Domain;

namespace NotificationRouting.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly INotificationService _service;

    public UsersController(INotificationService service)
    {
        _service = service;
    }

    [HttpPost]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public ActionResult<UserResponse> Create(CreateUserRequest request)
    {
        User user = _service.CreateUser(request.Name, request.AlertKeywords, request.WebhookUrl);
        return StatusCode(StatusCodes.Status201Created, UserResponse.FromDomain(user));
    }

    [HttpGet("{userId:guid}/messages")]
    [ProducesResponseType<IReadOnlyList<MessageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<MessageResponse>> GetMessages(Guid userId)
    {
        MessageResponse[] messages = _service.GetUserMessages(userId)
            .Select(entry => MessageResponse.FromDomain(entry.Message, entry.Status))
            .ToArray();
        return Ok(messages);
    }

    [HttpPut("{userId:guid}/messages/{messageId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public IActionResult MarkRead(Guid userId, Guid messageId)
    {
        OperationResult result = _service.MarkRead(userId, messageId);
        if (result.IsSuccess)
            return NoContent();

        var problem = new ProblemDetails
        {
            Title = result.ErrorKind == OperationErrorKind.NotFound ? "Message not found" : "Message state conflict",
            Detail = result.Error,
            Status = result.ErrorKind == OperationErrorKind.NotFound
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status409Conflict,
        };
        return StatusCode(problem.Status.Value, problem);
    }
}
