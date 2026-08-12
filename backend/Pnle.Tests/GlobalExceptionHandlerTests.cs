using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pnle.Api.Common;
using Xunit;

namespace Pnle.Tests.Api;

public class GlobalExceptionHandlerTests
{
    private static readonly GlobalExceptionHandler Handler =
        new(NullLogger<GlobalExceptionHandler>.Instance);

    [Fact]
    public async Task DbUpdateException_maps_to_conflict_with_generic_message()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var handled = await Handler.TryHandleAsync(
            httpContext,
            new DbUpdateException("unique constraint violation on email"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(
            StatusCodes.Status409Conflict,
            httpContext.Response.StatusCode);

        var body = await ReadBodyAsync(httpContext);

        Assert.DoesNotContain("unique constraint violation", body);
        Assert.Contains(
            "The request conflicts with the current state of the resource.",
            body);
    }

    [Fact]
    public async Task Unknown_exception_maps_to_500_with_generic_message()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var handled = await Handler.TryHandleAsync(
            httpContext,
            new InvalidOperationException("internal database credentials"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            httpContext.Response.StatusCode);

        var body = await ReadBodyAsync(httpContext);

        Assert.DoesNotContain("internal database credentials", body);
        Assert.Contains("An unexpected error occurred.", body);
    }

    private static async Task<string> ReadBodyAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;

        using var reader = new StreamReader(httpContext.Response.Body);

        return await reader.ReadToEndAsync();
    }
}
