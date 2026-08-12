using Microsoft.AspNetCore.Mvc;
using Pnle.Application.Common;

namespace Pnle.Api.Common;

public static class ResultHttpExtensions
{
    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess ? TypedResults.Ok() : MapFailure(result);

    public static IResult ToHttpResult<TValue>(
        this Result<TValue> result,
        Func<TValue, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : MapFailure(result);

    private static IResult MapFailure(Result result)
    {
        var error = result.Errors.FirstOrDefault();

        if (error is null || error.Type != ErrorType.Authentication)
        {
            return UnexpectedError();
        }

        return TypedResults.Unauthorized();
    }

    private static IResult UnexpectedError() =>
        TypedResults.Problem(
            title: "Unexpected error",
            detail: "An unexpected error occurred.",
            statusCode: StatusCodes.Status500InternalServerError);
}
