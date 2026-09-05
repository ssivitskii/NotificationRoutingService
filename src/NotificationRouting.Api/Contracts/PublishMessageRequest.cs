using NotificationRouting.Domain;
using System.ComponentModel.DataAnnotations;

namespace NotificationRouting.Api.Contracts;

public sealed record PublishMessageRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Title,
    [Required, StringLength(4000, MinimumLength = 1)] string Body,
    Importance Importance) : IValidatableObject
{
    private static readonly string[] ImportanceMemberNames = [nameof(Importance)];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);
        if (!Enum.IsDefined(Importance))
            yield return new ValidationResult("Importance is not valid.", ImportanceMemberNames);
    }
}
