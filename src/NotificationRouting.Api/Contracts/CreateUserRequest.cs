using System.ComponentModel.DataAnnotations;

namespace NotificationRouting.Api.Contracts;

public sealed record CreateUserRequest(
    [Required]
    [StringLength(100, MinimumLength = 1)] string Name,
    IReadOnlyList<string>? AlertKeywords,
    [StringLength(2048)] string? WebhookUrl) : IValidatableObject
{
    private static readonly string[] AlertKeywordsMemberNames = [nameof(AlertKeywords)];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);
        if (AlertKeywords is null)
            yield break;

        if (AlertKeywords.Count > 10)
        {
            yield return new ValidationResult(
                "At most 10 alert keywords are allowed.",
                AlertKeywordsMemberNames);
        }

        var uniqueKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string? keyword in AlertKeywords)
        {
            string trimmed = keyword?.Trim() ?? string.Empty;
            if (trimmed.Length is < 1 or > 50)
            {
                yield return new ValidationResult(
                    "Each alert keyword must contain between 1 and 50 characters after trimming.",
                    AlertKeywordsMemberNames);
                continue;
            }

            if (!uniqueKeywords.Add(trimmed))
            {
                yield return new ValidationResult(
                    "Alert keywords must be unique (case-insensitive).",
                    AlertKeywordsMemberNames);
            }
        }
    }
}
