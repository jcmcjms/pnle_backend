namespace Pnle.Application.Common;

public class Result
{
    protected Result(bool isSuccess, IReadOnlyList<Error> errors)
    {
        IsSuccess = isSuccess;
        Errors = errors;
    }

    public bool IsSuccess { get; }

    public IReadOnlyList<Error> Errors { get; }

    public bool IsFailure => !IsSuccess;

    public static Result Success() => new(true, []);

    public static Result Failure(params Error[] errors) => new(false, errors);

    public static Result Failure(IReadOnlyList<Error> errors) => new(false, errors);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, []);

    public static Result<TValue> Failure<TValue>(params Error[] errors) => new(default!, false, errors);

    public static Result<TValue> Failure<TValue>(IReadOnlyList<Error> errors) => new(default!, false, errors);
}
