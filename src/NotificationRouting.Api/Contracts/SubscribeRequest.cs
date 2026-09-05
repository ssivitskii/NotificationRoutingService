using NotificationRouting.Domain;
using System.ComponentModel.DataAnnotations;

namespace NotificationRouting.Api.Contracts;

public sealed record SubscribeRequest(Guid UserId, Importance MinimumImportance) : IValidatableObject
{
    private static readonly string[] ImportanceMemberNames = [nameof(MinimumImportance)];
    private static readonly string[] UserIdMemberNames = [nameof(UserId)];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);
        if (UserId == Guid.Empty)
            yield return new ValidationResult("UserId must not be empty.", UserIdMemberNames);
        if (!Enum.IsDefined(MinimumImportance))
            yield return new ValidationResult("MinimumImportance is not valid.", ImportanceMemberNames);
    }
}
