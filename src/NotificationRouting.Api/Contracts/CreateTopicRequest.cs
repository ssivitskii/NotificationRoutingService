using System.ComponentModel.DataAnnotations;

namespace NotificationRouting.Api.Contracts;

public sealed record CreateTopicRequest(
    [Required, StringLength(100, MinimumLength = 1)] string Name);
