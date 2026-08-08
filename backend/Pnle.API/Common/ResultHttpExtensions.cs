using Microsoft.AspNetCore.Mvc;
using Pnle.Application.Common;

namespace Pnle.Api.Common;

public static class ResultHttpExtensions
{
    public static IResult ToHttpResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return TypedResults.Ok();
        }

        return MapFailure(result);
    }

    public static IResult ToHttpResult<TValue>(
        this Result<TValue> result,
        Func<TValue, IResult> onSuccess)
    {
        if (result.IsSuccess)
        {
            return onSuccess(result.Value);
        }

        return MapFailure(result);
    }

    private static IResult MapFailure(Result result)
    {
        var error = result.Errors.FirstOrDefault();

        if (error is null)
        {
            return TypedResults.Problem(
                title: "Unexpected error",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        if (error.Code.StartsWith("AUTH_", StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.Unauthorized();
        }

        if (error.Code.StartsWith("VALIDATION_", StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad request",
                Detail = error.Message
            });
        }

        return TypedResults.Problem(
            title: "Unexpected error",
            detail: error.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
}
