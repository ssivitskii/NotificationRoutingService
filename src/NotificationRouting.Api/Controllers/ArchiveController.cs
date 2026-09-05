using Microsoft.AspNetCore.Mvc;
using NotificationRouting.Api.Contracts;
using NotificationRouting.Application.Abstractions;

namespace NotificationRouting.Api.Controllers;

[ApiController]
[Route("api/archive")]
public sealed class ArchiveController : ControllerBase
{
    private readonly INotificationService _service;

    public ArchiveController(INotificationService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MessageResponse>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<MessageResponse>> GetAll()
    {
        return Ok(_service.GetArchive().Select(message => MessageResponse.FromDomain(message)).ToArray());
    }
}
