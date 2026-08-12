using System.Security.Claims;
using Pnle.Api.Common;
using Pnle.Application.Auth;

namespace Pnle.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints
            .MapGroup("/auth")
            .WithTags("Auth")
            .RequireRateLimiting("auth");

        auth.MapPost("/google", LoginWithGoogle)
            .AllowAnonymous();

        auth.MapPost("/refresh", Refresh)
            .AllowAnonymous();

        auth.MapPost("/logout", Logout)
            .AllowAnonymous();

        auth.MapGet("/me", Me)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> LoginWithGoogle(
        LoginWithGoogleRequest request,
        LoginWithGoogleHandler handler,
        RefreshCookieWriter cookieWriter,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new LoginWithGoogleCommand(request.IdToken),
            cancellationToken);

        return result.ToHttpResult(session =>
        {
            cookieWriter.Set(
                httpContext,
                session.RefreshToken,
                session.RefreshTokenExpiresAtUtc);

            return TypedResults.Ok(new AccessTokenResponse(
                session.AccessToken,
                session.AccessTokenExpiresAtUtc,
                session.User));
        });
    }

    private static async Task<IResult> Refresh(
        HttpContext httpContext,
        RefreshSessionHandler handler,
        RefreshCookieWriter cookieWriter,
        CancellationToken cancellationToken)
    {
        var refreshToken = ReadRefreshToken(httpContext);

        if (refreshToken is null)
        {
            return TypedResults.Unauthorized();
        }

        var result = await handler.RefreshAsync(
            new RefreshSessionCommand(refreshToken),
            cancellationToken);

        if (result.IsFailure)
        {
            cookieWriter.Delete(httpContext);
            return result.ToHttpResult();
        }

        return result.ToHttpResult(session =>
        {
            cookieWriter.Set(
                httpContext,
                session.RefreshToken,
                session.RefreshTokenExpiresAtUtc);

            return TypedResults.Ok(new AccessTokenResponse(
                session.AccessToken,
                session.AccessTokenExpiresAtUtc,
                session.User));
        });
    }

    private static async Task<IResult> Logout(
        HttpContext httpContext,
        RefreshSessionHandler handler,
        RefreshCookieWriter cookieWriter,
        CancellationToken cancellationToken)
    {
        var refreshToken = ReadRefreshToken(httpContext);

        if (refreshToken is not null)
        {
            await handler.LogoutAsync(
                new RefreshSessionCommand(refreshToken),
                cancellationToken);
        }

        cookieWriter.Delete(httpContext);

        return TypedResults.NoContent();
    }

    private static IResult Me(ClaimsPrincipal user)
    {
        var sub = user.FindFirst(JwtClaimNames.Subject)?.Value;
        var email = user.FindFirst(JwtClaimNames.Email)?.Value;
        var name = user.FindFirst(JwtClaimNames.Name)?.Value;
        var picture = user.FindFirst(JwtClaimNames.Picture)?.Value;

        if (string.IsNullOrWhiteSpace(sub))
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(new
        {
            id = sub,
            email,
            name,
            picture
        });
    }

    private static string? ReadRefreshToken(HttpContext httpContext)
    {
        var refreshToken = httpContext.Request.Cookies[RefreshCookieWriter.RefreshCookieName];

        return string.IsNullOrWhiteSpace(refreshToken) ? null : refreshToken;
    }
}
