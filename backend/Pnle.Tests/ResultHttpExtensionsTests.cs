using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Pnle.Api.Common;
using Pnle.Application.Common;
using Xunit;

namespace Pnle.Tests.Api;

public class ResultHttpExtensionsTests
{
    [Fact]
    public void Authentication_error_maps_to_unauthorized()
    {
        var result = Result
            .Failure<object>(AuthErrors.InvalidRefreshToken)
            .ToHttpResult(_ => TypedResults.Ok());

        Assert.IsType<UnauthorizedHttpResult>(result);
        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            ((IStatusCodeHttpResult)result).StatusCode);
    }

    [Fact]
    public void Unexpected_error_maps_to_500_with_generic_detail()
    {
        var result = Result
            .Failure<object>(new Error(
                "INTERNAL_DETAILS",
                "sensitive failure detail",
                ErrorType.Unexpected))
            .ToHttpResult(_ => TypedResults.Ok());

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            ((IStatusCodeHttpResult)problem).StatusCode);
        Assert.Equal("An unexpected error occurred.", problem.ProblemDetails.Detail);
        Assert.DoesNotContain("sensitive failure detail", problem.ProblemDetails.Detail);
    }
}
