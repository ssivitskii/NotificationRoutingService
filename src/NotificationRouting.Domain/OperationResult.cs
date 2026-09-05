namespace NotificationRouting.Domain;

public readonly record struct OperationResult(bool IsSuccess, OperationErrorKind ErrorKind, string? Error)
{
    public static OperationResult Success()
    {
        return new OperationResult(true, OperationErrorKind.None, null);
    }

    public static OperationResult Failure(OperationErrorKind errorKind, string error)
    {
        if (errorKind == OperationErrorKind.None || !Enum.IsDefined(errorKind))
            throw new ArgumentOutOfRangeException(nameof(errorKind));

        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new OperationResult(false, errorKind, error);
    }
}
