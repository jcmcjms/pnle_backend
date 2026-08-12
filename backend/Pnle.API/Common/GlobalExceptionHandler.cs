using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Pnle.Api.Common;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var response = Map(exception);

        if (response.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled exception occurred: {ExceptionType}",
                exception.GetType().Name);
        }
        else
        {
            logger.LogWarning(
                exception,
                "Known exception handled: {ExceptionType}",
                exception.GetType().Name);
        }

        httpContext.Response.StatusCode = response.StatusCode;

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = response.StatusCode,
            Title = response.Title,
            Detail = response.Detail
        }, cancellationToken);

        return true;
    }

    private static ErrorResponse Map(Exception exception) => exception switch
    {
        DbUpdateException => new(
            StatusCodes.Status409Conflict,
            "Conflict",
            "The request conflicts with the current state of the resource."),

        _ => new(
            StatusCodes.Status500InternalServerError,
            "Unexpected error",
            "An unexpected error occurred.")
    };

    private sealed record ErrorResponse(
        int StatusCode,
        string Title,
        string Detail);
}
