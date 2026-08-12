namespace Pnle.Application.Common;

public sealed class Result<TValue>(TValue value, bool isSuccess, IReadOnlyList<Error> errors)
    : Result(isSuccess, errors)
{
    public TValue Value => IsSuccess
        ? value
        : throw new InvalidOperationException(
            "Cannot access the value of a failed Result.");
}
