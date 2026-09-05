using System.ComponentModel.DataAnnotations;

namespace NotificationRouting.Api.Contracts;

public sealed record CreateUserRequest(
    [Required]
    [StringLength(100, MinimumLength = 1)] string Name,
    IReadOnlyList<string>? AlertKeywords,
    [StringLength(2048)] string? WebhookUrl);
